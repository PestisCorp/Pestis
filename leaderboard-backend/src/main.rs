use log::{debug, info, trace};
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::io::{Read, Write};
use std::sync::Arc;
use tokio::sync::RwLock;
use warp::Filter;
use warp::hyper::body::Bytes;

#[derive(Clone, Serialize, Deserialize, Eq, PartialEq, Debug)]
struct Horde {
    rats: u64,
    id: u64,
}

#[derive(Clone, Serialize, Deserialize, Eq, PartialEq, Debug)]
struct POI {
    id: u64,
}

#[derive(Clone, Serialize, Deserialize, Eq, PartialEq, Debug)]
struct Player {
    id: PlayerID,
    username: String,
    score: u64,
    hordes: Vec<Horde>,
    pois: Vec<POI>,
    damage: u64,
}

#[derive(Serialize, Deserialize, Debug, Clone)]
struct Update {
    tick: u64,
    player: Player,
    fps: f32,
    timestamp: u64,
    room: String,
}

#[derive(Copy, Clone, PartialEq, Eq, Hash, Serialize, Deserialize, Debug)]
struct PlayerID(u64);

#[derive(Serialize, Deserialize, Debug, Clone)]
struct Config {
    players_per_room: usize,
    max_bots_per_client: usize,
}

#[derive(Serialize, Debug, Clone)]
struct Room {
    name: String,
    players: Vec<String>,
    /// The config that the room was created with
    config: Config,
    active: bool,
}

#[derive(Serialize)]
struct RoomResponse {
    name: String,
    config: Config,
}

#[derive(Serialize, Debug)]
struct State {
    rooms: Vec<Room>,
}

#[derive(Serialize, Debug)]
struct Info {
    /// The config new rooms should be created with
    config: Config,
    state: State,
}

#[derive(Serialize, Clone)]
enum CommandType {
    Restart,
}

#[derive(Serialize, Clone)]
struct Command {
    command_type: CommandType,
    room: String,
    nonce: usize,
}

#[derive(Clone)]
struct LeaderboardManager {
    players: Arc<RwLock<HashMap<String, Player>>>,
    history: Arc<RwLock<HashMap<String, Vec<Update>>>>,
    info: Arc<RwLock<Info>>,
    commands: Arc<RwLock<Vec<Command>>>,
}

impl LeaderboardManager {
    fn new() -> Self {
        let filename = chrono::Utc::now().format("%Y-%m-%d");
        let data_path = std::env::var("DATA_PATH").unwrap_or(".".to_string());
        let filename = format!("{data_path}/{filename}-leaderboard.json");
        let history = if let Ok(mut file) = std::fs::File::open(&filename) {
            let mut contents = String::new();
            file.read_to_string(&mut contents).unwrap();
            serde_json::from_str(&contents).unwrap()
        } else {
            HashMap::new()
        };
        LeaderboardManager {
            players: Arc::new(RwLock::new(HashMap::new())),
            history: Arc::new(RwLock::new(history)),
            info: Arc::new(RwLock::new(Info {
                config: Config {
                    players_per_room: 100,
                    max_bots_per_client: 25,
                },
                state: State { rooms: vec![] },
            })),
            commands: Arc::new(RwLock::new(Vec::new())),
        }
    }

    async fn get_sorted_players(&self) -> Vec<Player> {
        let players = self.players.read().await;
        let mut players: Vec<Player> = players.values().map(|player| player.clone()).collect();
        players.sort_by_key(|p| p.score);
        players.reverse();
        players
    }

    async fn get_alltime_leaderboard(&self) -> Vec<Player> {
        let history = self.history.read().await;
        // Get the update for each player where they had their highest score
        let mut players: Vec<Player> = history
            .iter()
            .filter_map(|(_, updates)| updates.iter().max_by_key(|update| update.player.score))
            .map(|update| update.player.clone())
            .collect();
        players.sort_by_key(|p| p.score);
        players.reverse();
        players
    }

    async fn median_fps(&self) -> f32 {
        let history = self.history.read().await;
        let fps: Vec<f32> = history
            .values()
            .filter_map(|updates| updates.iter().last().map(|update| update.fps))
            .collect();
        let mut fps = fps;
        fps.sort_by(|a, b| a.partial_cmp(b).unwrap());
        let len = fps.len();
        if len == 0 {
            0.0
        } else if len % 2 == 0 {
            (fps[len / 2] + fps[len / 2 + 1]) / 2.0
        } else {
            fps[len / 2]
        }
    }

    async fn add_player(&self, id: u64, username: String) {
        let mut players = self.players.write().await;
        players.insert(
            username.clone(),
            Player {
                id: PlayerID(id),
                username,
                score: 0,
                hordes: vec![],
                pois: vec![],
                damage: 0,
            },
        );
    }

    async fn remove_idle_players(&self) {
        let players_lock = self.players.read().await;
        let players = players_lock.keys().cloned().collect::<Vec<String>>();
        drop(players_lock);
        let mut to_remove = Vec::new();
        let history = self.history.read().await;

        let current_timestamp = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_secs();

        for player in players {
            let latest_update = history.get(&player).and_then(|updates| updates.last());
            if let Some(update) = latest_update {
                // If the player hasn't updated in 2 minutes, remove them
                if current_timestamp - update.timestamp > 120 {
                    to_remove.push(player.clone());
                }
            } else {
                to_remove.push(player.clone());
            }
        }

        drop(history);
        let mut players = self.players.write().await;
        players.retain(|_, player| !to_remove.contains(&player.username));

        drop(players);
        let mut info = self.info.write().await;
        for room in &mut info.state.rooms {
            room.players
                .retain(|player| !to_remove.contains(&player));
        }
    }

    async fn save_to_file(&self) {
        let current_date = chrono::Utc::now().format("%Y-%m-%d");
        let history = self.history.read().await;
        let data_path = std::env::var("DATA_PATH").unwrap_or(".".to_string());
        let filename = format!("{data_path}/{current_date}-leaderboard.json");
        let mut file = std::fs::File::create(filename).unwrap();
        let data = serde_json::to_string(&*history).unwrap();
        file.write_all(data.as_bytes()).unwrap();
    }

    /// Get a room name and config for a new player to join, or create a new room if none are available
    async fn get_or_create_room(&self) -> RoomResponse {
        let mut info = self.info.write().await;

        for room in &info.state.rooms {
            if room.players.len() < info.config.players_per_room && room.active {
                return RoomResponse {
                    name: room.name.clone(),
                    config: room.config.clone(),
                };
            }
        }

        let config = info.config.clone();

        let room_name = format!("Room {}", info.state.rooms.len());
        let room = Room {
            name: room_name.clone(),
            players: vec![],
            config: config.clone(),
            active: true,
        };
        info.state.rooms.push(room);

        RoomResponse {
            name: room_name.clone(),
            config,
        }
    }

    /// Called by the client when they leave the game
    async fn player_leave(&self, username: &str) {
        let mut players = self.players.write().await;
        players.remove(username);

        // Drop lock
        drop(players);

        // Remove the player from all rooms
        let mut info = self.info.write().await;
        for room in &mut info.state.rooms {
            room.players.retain(|player| player != username);
        }
    }

    async fn restart_room(&self, room_name: String) -> bool {
        let mut commands = self.commands.write().await;
        let nonce = commands.len();
        commands.push(Command {
            command_type: CommandType::Restart,
            room: room_name.clone(),
            nonce,
        });

        drop(commands);

        // Deactivate the room
        let mut info = self.info.write().await;
        for room in &mut info.state.rooms {
            if room.name == room_name {
                room.active = false;
                return true;
            }
        }

        false
    }

    async fn get_commands_for_room(&self, room: String, last_received_nonce: i64) -> Vec<Command> {
        let commands = self.commands.read().await;
        commands
            .iter()
            .filter(|command| {
                (command.room == room)
                    & (last_received_nonce == -1 || command.nonce > last_received_nonce as usize)
            })
            .map(|command| command.clone())
            .collect()
    }

    async fn get_rooms(&self) -> Vec<Room> {
        let info = self.info.read().await;
        info.state.rooms.clone()
    }
}

#[derive(serde::Deserialize, Debug)]
struct JoinRequest {
    username: String,
    id: u64,
}

async fn join(
    body: JoinRequest,
    manager: LeaderboardManager,
) -> Result<impl warp::Reply, warp::Rejection> {
    manager.add_player(body.id, body.username).await;
    Ok(warp::reply::with_status("ok", warp::http::StatusCode::OK))
}

/// Get the current leaderboard: GET /api/leaderboard
async fn get_leaderboard(manager: LeaderboardManager) -> Result<impl warp::Reply, warp::Rejection> {
    let leaderboard = manager.get_sorted_players().await;
    Ok(warp::reply::json(&leaderboard))
}

async fn get_alltime_leaderboard(
    manager: LeaderboardManager,
) -> Result<impl warp::Reply, warp::Rejection> {
    let leaderboard = manager.get_alltime_leaderboard().await;
    Ok(warp::reply::json(&leaderboard))
}

async fn get_median_fps(manager: LeaderboardManager) -> Result<impl warp::Reply, warp::Rejection> {
    let fps = manager.median_fps().await;
    Ok(warp::reply::json(&fps))
}

/// Get the current c&c info: GET /api/info
async fn get_info(manager: LeaderboardManager) -> Result<impl warp::Reply, warp::Rejection> {
    let info = manager.info.read().await;
    Ok(warp::reply::json(&*info))
}

/// Get a room name to join: GET /api/room
async fn get_or_create_room(
    manager: LeaderboardManager,
) -> Result<impl warp::Reply, warp::Rejection> {
    let room = manager.get_or_create_room().await;

    Ok(warp::reply::json(&room))
}

/// Restart a room: POST /api/restart {room: String}
async fn restart_room(
    room: String,
    manager: LeaderboardManager,
) -> Result<impl warp::Reply, warp::Rejection> {
    let res = manager.restart_room(room).await;
    Ok(warp::reply::with_status(
        res.to_string(),
        warp::http::StatusCode::OK,
    ))
}

/// Get commands for a room: GET /api/commands {room: String, last_received_nonce: usize}
/// Returns a list of commands that have been issued since the last received nonce
async fn get_commands_for_room(
    room: String,
    last_received_nonce: i64,
    manager: LeaderboardManager,
) -> Result<impl warp::Reply, warp::Rejection> {
    let commands = manager
        .get_commands_for_room(room, last_received_nonce)
        .await;
    Ok(warp::reply::json(&commands))
}

/// List all rooms: GET /api/rooms
async fn get_rooms(manager: LeaderboardManager) -> Result<impl warp::Reply, warp::Rejection> {
    let rooms = manager.get_rooms().await;
    Ok(warp::reply::json(&rooms))
}

/// Notify that the client has left the game: POST /api/leave {username: String}
async fn leave(
    username: String,
    manager: LeaderboardManager,
) -> Result<impl warp::Reply, warp::Rejection> {
    manager.player_leave(&username).await;
    Ok(warp::reply::with_status("ok", warp::http::StatusCode::OK))
}

async fn update_player(
    update: Update,
    manager: LeaderboardManager,
) -> Result<impl warp::Reply, warp::Rejection> {
    trace!("Received Update: {update:?}");

    let mut players = manager.players.write().await;
    players.insert(update.player.username.clone(), update.player.clone());
    drop(players);
    let mut history = manager.history.write().await;
    let player_history = history
        .entry(update.player.username.clone())
        .or_insert(vec![]);

    // Only add the update if the player has changed
    if player_history.is_empty() || player_history.last().unwrap().player != update.player {
        player_history.push(update.clone());
    }

    // Add player to room if not already in it
    drop(history);
    trace!("Received update: {update:?}");
    let mut info = manager.info.write().await;
    let rooms = &mut info.state.rooms;
    for room in rooms {
        if &room.name == &update.room {
            if !room
                .players
                .iter()
                .any(|player| *player == update.player.username)
            {
                room.players.push(update.player.username.clone());
                debug!("Added player {} to room {}", update.player.username, room.name);
            }
            break;
        }
    }
    Ok(warp::reply::with_status("ok", warp::http::StatusCode::OK))
}

#[tokio::main]
async fn main() {
    env_logger::init();

    let manager = LeaderboardManager::new();

    let manager_clone = manager.clone();
    // Notify that the client has joined the session: POST /api/join {username: String}
    let join = warp::post()
        .and(warp::path("api"))
        .and(warp::path("join"))
        .and(warp::body::json())
        .and(warp::body::content_length_limit(1024 * 16))
        .and_then(move |body| {
            let manager = manager_clone.clone();
            async move { join(body, manager).await }
        });

    let manager_clone = manager.clone();
    // Get the current leaderboard: GET /api/leaderboard
    let leaderboard = warp::get()
        .and(warp::path("api"))
        .and(warp::path("leaderboard"))
        .and_then(move || {
            let manager = manager_clone.clone();
            async move { get_leaderboard(manager).await }
        });

    let manager_clone = manager.clone();
    // Get the current leaderboard: GET /api/leaderboard
    let alltime_leaderboard = warp::get()
        .and(warp::path("api"))
        .and(warp::path("alltime"))
        .and_then(move || {
            let manager = manager_clone.clone();
            async move { get_alltime_leaderboard(manager).await }
        });

    let manager_clone = manager.clone();
    // Notify that the client has joined the session: POST /api/join {username: String}
    let update = warp::post()
        .and(warp::path("api"))
        .and(warp::path("update"))
        .and(warp::body::json())
        .and(warp::body::content_length_limit(1024 * 16))
        .and_then(move |body| {
            let manager = manager_clone.clone();
            async move { update_player(body, manager).await }
        });

    let manager_clone = manager.clone();
    let median_fps = warp::get()
        .and(warp::path("api"))
        .and(warp::path("fps"))
        .and_then(move || {
            let manager = manager_clone.clone();
            async move { get_median_fps(manager).await }
        });

    let manager_clone = manager.clone();
    let info_handler = warp::get()
        .and(warp::path("api"))
        .and(warp::path("info"))
        .and_then(move || {
            let manager = manager_clone.clone();
            async move { get_info(manager).await }
        });

    let manager_clone = manager.clone();
    let room_handler = warp::get()
        .and(warp::path("api"))
        .and(warp::path("room"))
        .and_then(move || {
            let manager = manager_clone.clone();
            async move { get_or_create_room(manager).await }
        });

    let manager_clone = manager.clone();
    let restart = warp::post()
        .and(warp::path("api"))
        .and(warp::path("restart"))
        .and(warp::body::json())
        .and_then(move |body: serde_json::Value| {
            let room = body["room"].as_str().unwrap().to_string();
            let manager = manager_clone.clone();
            async move { restart_room(room, manager).await }
        });

    let manager_clone = manager.clone();
    let get_commands = warp::get()
        .and(warp::path("api"))
        .and(warp::path("commands"))
        .and(warp::body::json())
        .and_then(move |body: serde_json::Value| {
            let room = body.get("room").unwrap().as_str().unwrap().to_string();
            let last_received_nonce = body.get("last_received_nonce").unwrap().as_i64().unwrap();
            let manager = manager_clone.clone();
            async move { get_commands_for_room(room, last_received_nonce, manager).await }
        });

    let manager_clone = manager.clone();
    let leave = warp::post()
        .and(warp::path("api"))
        .and(warp::path("leave"))
        .and(warp::body::json())
        .and_then(move |body: serde_json::Value| {
            let username = body["username"].as_str().unwrap().to_string();
            let manager = manager_clone.clone();
            async move { leave(username, manager).await }
        });

    let manager_clone = manager.clone();
    let rooms = warp::get()
        .and(warp::path("api"))
        .and(warp::path("rooms"))
        .and_then(move || {
            let manager = manager_clone.clone();
            async move { get_rooms(manager).await }
        });

    let handler = join
        .or(leaderboard)
        .or(alltime_leaderboard)
        .or(update)
        .or(median_fps)
        .or(info_handler)
        .or(room_handler)
        .or(restart)
        .or(get_commands)
        .or(leave)
        .or(rooms)
        .with(warp::log("pestis::api"))
        .with(
            warp::cors()
                .allow_any_origin()
                .allow_methods(vec!["GET", "POST", "OPTIONS"])
                .allow_header("content-type"),
        );

    info!("Starting managements tasks");

    tokio::task::spawn(async move {
        loop {
            tokio::time::sleep(tokio::time::Duration::from_secs(120)).await;
            debug!("Running management tasks");
            manager.remove_idle_players().await;
            debug!("Finished cleaning idle players");
            manager.save_to_file().await;
            debug!("Saved leaderboard to file");
        }
    });

    info!("Starting server");

    warp::serve(handler).run(([0, 0, 0, 0], 8081)).await
}

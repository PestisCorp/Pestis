use log::{debug, info};
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::io::Read;
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

#[derive(Serialize, Deserialize, Debug)]
struct Update {
    tick: u64,
    player: Player,
    fps: f32,
    timestamp: u64,
}

#[derive(Copy, Clone, PartialEq, Eq, Hash, Serialize, Deserialize, Debug)]
struct PlayerID(u64);

#[derive(Clone)]
struct LeaderboardManager {
    players: Arc<RwLock<HashMap<PlayerID, Player>>>,
    history: Arc<RwLock<HashMap<PlayerID, Vec<Update>>>>,
}

impl LeaderboardManager {
    fn new() -> Self {
        LeaderboardManager {
            players: Arc::new(RwLock::new(HashMap::new())),
            history: Arc::new(RwLock::new(HashMap::new())),
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
            PlayerID(id),
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
        let players = players_lock.keys().cloned().collect::<Vec<PlayerID>>();
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
        players.retain(|_, player| !to_remove.contains(&player.id));
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

async fn update_player(
    update: Update,
    manager: LeaderboardManager,
) -> Result<impl warp::Reply, warp::Rejection> {
    let mut players = manager.players.write().await;
    players.insert(update.player.id, update.player.clone());
    drop(players);
    let mut history = manager.history.write().await;
    let player_history = history.entry(update.player.id).or_insert(vec![]);

    // Only add the update if the player has changed
    if player_history.is_empty() || player_history.last().unwrap().player != update.player {
        player_history.push(update);
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

    let handler = join
        .or(leaderboard)
        .or(alltime_leaderboard)
        .or(update)
        .or(median_fps)
        .with(warp::log("pestis::api"))
        .with(
            warp::cors()
                .allow_any_origin()
                .allow_methods(vec!["GET", "POST", "OPTIONS"])
                .allow_header("content-type"),
        );

    info!("Starting cleanup task");

    tokio::task::spawn(async move {
        loop {
            tokio::time::sleep(tokio::time::Duration::from_secs(120)).await;
            debug!("Cleaning up idle players");
            manager.remove_idle_players().await;
            debug!("Finished cleaning idle players");
        }
    });

    info!("Starting server");

    warp::serve(handler).run(([0, 0, 0, 0], 8081)).await
}

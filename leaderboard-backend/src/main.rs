use log::info;
use serde::Serialize;
use std::collections::HashMap;
use std::sync::Arc;
use tokio::sync::RwLock;
use warp::Filter;

#[derive(Clone, Serialize)]
struct Horde {
    rats: u64,
    id: u64,
}

#[derive(Clone, Serialize)]
struct POI {
    id: u64,
}

#[derive(Clone, Serialize)]
struct Player {
    id: PlayerID,
    username: String,
    score: u64,
    hordes: Vec<Horde>,
    pois: Vec<POI>,
}

#[derive(Copy, Clone, PartialEq, Eq, Hash, Serialize)]
struct PlayerID(u64);

#[derive(Clone)]
struct LeaderboardManager {
    players: Arc<RwLock<HashMap<PlayerID, Player>>>,
}

impl LeaderboardManager {
    fn new() -> Self {
        LeaderboardManager {
            players: Arc::new(RwLock::new(HashMap::new())),
        }
    }

    async fn get_sorted_players(&self) -> Vec<Player> {
        let players = self.players.read().await;
        let mut players: Vec<Player> = players.values().map(|player| player.clone()).collect();
        players.sort_by_key(|p| p.score);
        players.reverse();
        players
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
            },
        );
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

    let handler = join
        .or(leaderboard)
        .with(warp::log("pestis::api")).with(
        warp::cors()
            .allow_any_origin()
            .allow_methods(vec!["GET", "POST", "OPTIONS"])
            .allow_header("content-type"),
    );

    info!("Starting server");

    warp::serve(handler).run(([0, 0, 0, 0], 8081)).await
}

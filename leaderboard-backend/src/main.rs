use warp::Filter;
use log::info;

#[derive(serde::Deserialize, Debug)]
struct JoinRequest {
    username: String,
}

async fn join(body: JoinRequest) -> Result<impl warp::Reply, warp::Rejection> {
    Ok((warp::http::StatusCode::OK, "Hello, World!"))
}

#[tokio::main]
async fn main() {
    env_logger::init();

    // Notify that the client has joined the session: POST /api/join {username: String}
    let join = warp::post()
        .and(warp::path("api"))
        .and(warp::path("join"))
        .and(warp::body::json())
        .and(warp::body::content_length_limit(1024 * 16))
        .and_then(join);


    let handler = join
        .with(warp::log("pestis::api"))
        .with(
            warp::cors()
                .allow_any_origin()
                .allow_methods(vec!["GET", "POST", "OPTIONS"])
                .allow_header("content-type"),
        );

    info!("Starting server");

    warp::serve(handler).run(([0, 0, 0, 0], 8081)).await
}
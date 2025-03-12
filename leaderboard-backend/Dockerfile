FROM rust:latest

ADD . /app
WORKDIR /app

RUN cargo build --release

ENV RUST_LOG="leaderboard=debug,warp=debug"

ENTRYPOINT ["/app/target/release/leaderboard"]
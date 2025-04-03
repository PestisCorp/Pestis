FROM rust:latest

ADD ./src /app/src
ADD ./Cargo.toml /app/Cargo.toml
WORKDIR /app

RUN cargo build --release

ENV RUST_LOG="leaderboard=debug,warp=debug"
ENV DATA_PATH=/data

ENTRYPOINT ["/app/target/release/leaderboard"]
import { BACKEND_URL } from '$env/static/private';
import type { Player } from '$lib/types';

export const load = async () => {
	let resp = await fetch(`${BACKEND_URL}/api/leaderboard`);
	const leaderboard: Player[] = await resp.json();

	resp = await fetch(`${BACKEND_URL}/api/alltime`);
	const allTime: Player[] = await resp.json();

	resp = await fetch(`${BACKEND_URL}/api/fps`);
	const fps: number = await resp.json();
	return { leaderboard, allTime, fps };
};

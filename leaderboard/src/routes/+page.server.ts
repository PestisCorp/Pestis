import { BACKEND_URL } from '$env/static/private';

export const load = async () => {
	const resp = await fetch(`${BACKEND_URL}/api/leaderboard`);
	const leaderboard = await resp.json();
	console.log(leaderboard);
	return { leaderboard };
};

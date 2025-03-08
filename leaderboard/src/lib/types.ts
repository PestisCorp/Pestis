export interface Horde {
	id: string;
	rats: number;
}

export interface POI {
	id: string;
}

export interface Player {
	username: string;
	score: number;
	hordes: Horde[];
	pois: POI[];
	id: number;
}

export interface PageData {
	leaderboard: Player[];
}

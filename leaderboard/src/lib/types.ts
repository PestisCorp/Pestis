export interface Horde {
	id: string;
	rats: number;
}

export interface POI {
	id: string;
}

export interface Player {
	name: string;
	score: number;
	hordes: Horde[];
	pois: POI[];
}

export interface PageData {
	leaderboard: Player[];
}

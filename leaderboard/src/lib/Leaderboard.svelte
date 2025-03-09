<script lang="ts">
	import type { Player } from '$lib/types';

	export let leaderboard: Player[] = [];
</script>

<div class="w-full border-2 mx-auto text-left h-5/6 overflow-scroll">
	<div class="h-10 align-baseline flex w-full font-bold text-left pl-3">
		<p class="flex-1 pr-1">Rank</p>
		<p class="flex-3">Username</p>
		<p class="flex-2">Score</p>
		<p class="flex-2">Rats</p>
		<p class="flex-2">Hordes</p>
		<p class="flex-2">POIs</p>
	</div>

	{#each leaderboard as player, i (player.id)}
		<div class="align-baseline flex text-left pl-3 {i % 2 === 0 ? 'text-gray-50': 'text-slate-300'}">
			<p class="flex-1 pr-1">{i + 1}.</p>
			<p class="flex-3">{player.username}</p>
			<p class="flex-2">{player.score}</p>
			<p class="flex-2">{player.hordes.length === 0 ? 0 : player.hordes.map(horde => horde.rats).reduce((sum, rats) => sum + rats)}</p>
			<p class="flex-2">{player.hordes.length}</p>
			<p class="flex-2">{player.pois.length}</p>
		</div>
	{/each}
</div>
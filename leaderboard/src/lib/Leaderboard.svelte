<script lang="ts">
	import type { Player } from '$lib/types';

	export let leaderboard: Player[] = [];
</script>

<div class="w-full border-2 mx-auto text-left h-5/6 overflow-y-scroll flex">
	<!--	<div class="h-10 align-baseline flex w-full font-bold text-left pl-3">-->
	<!--		<p class="flex-1 pr-1 shrink-0">Rank</p>-->
	<!--		<p class="flex-3 shrink-0">Name</p>-->
	<!--		<p class="flex-2 shrink-0">Score</p>-->
	<!--		<p class="flex-2 shrink-0">Rats</p>-->
	<!--		<p class="flex-1 pr-3 shrink-0">Hordes</p>-->
	<!--		<p class="flex-1 pr-3 shrink-0">POIs</p>-->
	<!--		<p class="flex-3 shrink-0">Damage</p>-->
	<!--	</div>-->

	<!--	{#each leaderboard as player, i (player.id)}-->
	<!--		<div class="align-baseline flex text-left pl-3 {i % 2 === 0 ? 'text-gray-50': 'text-slate-300'}">-->
	<!--			<p class="flex-1 pr-1 shrink-0">{i + 1}.</p>-->
	<!--			<p class="flex-3 shrink-0">{player.username}</p>-->
	<!--			<p class="flex-2 shrink-0">{player.score}</p>-->
	<!--			<p class="flex-2 shrink-0">{player.hordes.length === 0 ? 0 : player.hordes.map(horde => horde.rats).reduce((sum, rats) => sum + rats)}</p>-->
	<!--			<p class="flex-2 shrink-0">{player.hordes.length}</p>-->
	<!--			<p class="flex-2 shrink-0">{player.pois.length}</p>-->
	<!--			<p class="flex-2 shrink-0">{player.damage}</p>-->
	<!--		</div>-->
	<!--	{/each}-->

	<div class="leaderboard-column">
		<p class="leaderboard-header">Rank</p>
		{#each leaderboard as player, i (player.username)}
			<p class="leaderboard-element {i % 2 === 0 ? 'text-gray-50': 'text-slate-300'}">{i + 1}.</p>
		{/each}
	</div>
	<div class="leaderboard-column flex-3 overflow-x-hidden">
		<p class="leaderboard-header">Name</p>
		{#each leaderboard as player, i (player.username)}
			<p class="leaderboard-element {i % 2 === 0 ? 'text-gray-50': 'text-slate-300'}">{player.username }</p>
		{/each}
	</div>
	<div class="leaderboard-column">
		<p class="leaderboard-header">Score</p>
		{#each leaderboard as player, i (player.username)}
			<p class="leaderboard-element {i % 2 === 0 ? 'text-gray-50': 'text-slate-300'}">{player.score}</p>
		{/each}
	</div>
	<div class="leaderboard-column">
		<p class="leaderboard-header">Rats</p>
		{#each leaderboard as player, i (player.username)}
			<p class="leaderboard-element {i % 2 === 0 ? 'text-gray-50': 'text-slate-300'}">
				{player.hordes.length === 0 ? 0 : player.hordes.map(horde => horde.rats).reduce((sum, rats) => sum + rats)}
			</p>
		{/each}
	</div>
	<div class="leaderboard-column">
		<p class="leaderboard-header">Hordes</p>
		{#each leaderboard as player, i (player.username)}
			<p class="leaderboard-element {i % 2 === 0 ? 'text-gray-50': 'text-slate-300'}">
				{player.hordes.length}
			</p>
		{/each}
	</div>
	<div class="leaderboard-column">
		<p class="leaderboard-header">POIs</p>
		{#each leaderboard as player, i (player.username)}
			<p class="leaderboard-element {i % 2 === 0 ? 'text-gray-50': 'text-slate-300'}">
				{player.pois.length}
			</p>
		{/each}
	</div>
	<div class="leaderboard-column">
		<p class="leaderboard-header">Damage</p>
		{#each leaderboard as player, i (player.username)}
			<p class="leaderboard-element {i % 2 === 0 ? 'text-gray-50': 'text-slate-300'}">
				{player.damage}
			</p>
		{/each}
	</div>
</div>
<script lang="ts">
	import type { Player } from '$lib/types';

	export let leaderboard: Player[] = [];
</script>

<table class="w-full border-2 mx-auto text-left h-5/6">
	<thead>
	<tr>
		<th class="p-3">Rank</th>
		<th>Username</th>
		<th>Score</th>
		<th>Rats</th>
		<th>Hordes</th>
		<th>POIs</th>
	</tr>
	</thead>
	<tbody>
	{#each leaderboard as player, i (player.id)}
		<tr>
			<td class="p-3">{i + 1}.</td>
			<td>{player.username}</td>
			<td>{player.score}</td>
			<td>{player.hordes.map(horde => horde.rats).reduce((sum, rats) => sum + rats)}</td>
			<td>{player.hordes.length}</td>
			<td>{player.pois.length}</td>

		</tr>
	{/each}
	</tbody>
</table>
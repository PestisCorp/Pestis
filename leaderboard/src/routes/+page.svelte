<script lang="ts">
	import type { PageProps } from './$types';

	let { data }: PageProps = $props();
	console.log(data);
</script>

<div class="bg-slate-800 flex min-h-screen min-w-screen text-gray-300">
	<div class="flex-1 p-5 block">
		<h1 class="text-3xl pt-30 underline mx-auto text-center mb-5">Leaderboard</h1>
		<table class="w-full border-2 mx-auto text-left">
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
			{#each data.leaderboard as player, i (player.id)}
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
	</div>
	<div class="flex-1 flex-row min-h-screen">
		<div class="flex-1 p-10">
			<img src="/pestis.png" alt="Pestis" class="w-64 aspect-square mx-auto" />
		</div>
		<div class="flex-1 flex justify-center">
			<h1 class="text-4xl">Rat-based Domination!</h1>
		</div>
	</div>

	<div class="flex-1 p-5 flex justify-center">
		<h1 class="text-3xl pt-30 underline">Highlighted Player</h1>
	</div>
</div>

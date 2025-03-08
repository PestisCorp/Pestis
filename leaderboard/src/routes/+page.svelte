<script lang="ts">
	import type { PageProps } from './$types';
	import type { Player } from '$lib/types';

	import Marquee from 'svelte-fast-marquee';

	let { data }: PageProps = $props();
	let leaderboard: Player[] = data.leaderboard;
</script>

<div class="bg-slate-800 min-h-screen min-w-screen text-gray-300">
	<Marquee direction="right" class="pt-3">
		{#each Array(100).keys() as _ (_)}
			<img src="/rat_right.png" alt="rat" class="ml-2" />
		{/each}
	</Marquee>
	<div class="flex">
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
	<Marquee direction="left" class="pb-3 absolute bottom-15">
		{#each Array(100).keys() as _ (_)}
			<img src="/rat_right.png" alt="rat" class="ml-2 rotate-z-180 rotate-x-180" />
		{/each}
	</Marquee>
</div>

<script lang="ts">
	import type { PageProps } from './$types';

	import Marquee from 'svelte-fast-marquee';
	import Leaderboard from '$lib/Leaderboard.svelte';
	import { onMount } from 'svelte';

	let { data }: PageProps = $props();

	onMount(() => {
		// Reload the page every minute
		setTimeout(() => {
			location.reload();
		}, 30000);
	});

</script>

<div class="bg-slate-800 min-h-screen min-w-screen max-w-screen text-gray-300 flex-wrap">
	<Marquee direction="right" class="pt-3">
		{#each Array(100).keys() as _ (_)}
			<img src="/rat_right.png" alt="rat" class="ml-2" />
		{/each}
	</Marquee>
	<div class="flex">
		<div class="flex-2 pl-5 block min-w-0 pr-0">
			<h1 class="text-3xl pt-3 underline mx-auto text-center mb-5">Current</h1>
			<Leaderboard leaderboard={data.leaderboard} />
		</div>
		<div class="flex-1 flex-row min-h-screen">
			<div class="flex-1 p-10">
				<img src="/pestis.png" alt="Pestis" class="w-64 aspect-square mx-auto" />
			</div>
			<div class="flex-1 text-center">
				<h1 class="text-4xl px-0">Rat-based Domination!</h1>
				<h2 class="mt-5 text-2xl">
					Current
					Rats: {data.leaderboard.length === 0 ? 0 : (data.leaderboard.map(player => player.hordes.length === 0 ? 0 : player.hordes.map(horde => horde.rats).reduce((sum, rats) => sum + rats)).reduce((sum, rats) => sum + rats))}
				</h2>
				<h2 class="mt-5 text-2xl">
					Current
					Hordes: {data.leaderboard.length === 0 ? 0 : (data.leaderboard.map(player => player.hordes.length).reduce((sum, hordes) => sum + hordes))}
				</h2>
				<h2 class="mt-5 text-2xl">
					Current
					Players: {data.leaderboard.length}
				</h2>
				<h2 class="mt-5 text-2xl">
					Median
					FPS: {data.fps.toFixed(0)}
				</h2>
				<div class="mt-50">
					<a href="https://pestis.murraygrov.es/download/latest" class="underline">Download game</a>
				</div>
			</div>
		</div>

		<div class="flex-2 pr-5 text-center min-w-0 pl-0">
			<h1 class="text-3xl pt-3 underline mx-auto text-center mb-5">Best of Today</h1>
			<Leaderboard leaderboard={data.allTime} />
		</div>
	</div>
	<Marquee direction="left" class="pb-3 absolute bottom-15">
		{#each Array(100).keys() as _ (_)}
			<img src="/rat_right.png" alt="rat" class="ml-2 rotate-z-180 rotate-x-180" />
		{/each}
	</Marquee>
</div>

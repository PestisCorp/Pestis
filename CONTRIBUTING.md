# Development
- Pick issue from kanban "Ready"
- Move to "In Progress"
- In the issue click "Create a branch" under "Development", it should branch off Main
- Make a Draft PR for the branch.
- Do your work, ensuring you use the principal of Atomic Commits - this means each commit should represent *one* individual unit of work. If in doubt, lots of small commits is better than one big one!
  It makes reviewing PRs are reversing changes easier. It also reduces the chance of merge conflicts.
- When you're done, type up details about what you changed in the PR and mark it as non-draft.
- Request 2 reviewers
- When reviewed, merge into main!

# Releases
When we're ready to do a release, we'll create a GitHub [Release](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository) from main. A GitHub Release is a good way of marking a specific version of the repo's history.

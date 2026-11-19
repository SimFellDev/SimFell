# AI usage policy

SimFell accepts contributions written with AI tools, and the contributor is responsible for every part of them. This
policy applies to code, tests, documentation, action lists, issues, pull request descriptions and review replies.

SimFell's maintainers use AI coding agents. The `CLAUDE.md` file has the agents' instructions for this repository, and
the `.claude/` folder has the project's agents and skills. Every contributor can use them.

## Responsibility

- You are the author of everything you submit. Read every line before you open the pull request.
- Build the change and run the tests yourself. A test result in the description is one you saw.
- Answer review questions from your own understanding of the change.
- A person opens each issue and pull request. An agent's output has a person's review before it is submitted.

## Game values and mechanics

An AI model's answer is no source for a game value. Every constant, multiplier, period, chance and cap names its source
in its comment: a game data record, a tooltip, or a combat log pattern with its fights.

- Check each value an AI tool gives against that source before you commit it.
- Mark a value no log has confirmed with `UNVERIFIED`.
- Give an accuracy claim its numbers: the `HeroAccuracyTests` result before and after the change.

For the rules of evidence, see [Replay accuracy](docs/REPLAY_ACCURACY.md#evidence).

## Issues, descriptions and review replies

- Check every sentence of AI-written text against the code and the logs before you post it.
- Keep the text to the facts a reviewer needs: the subject, the evidence and the result.
- Report a problem you reproduced yourself, with its profile or its FellowshipLogs report link.

## Licensing

- You have the right to submit the contribution under the [GNU Affero General Public License v3.0](LICENSE).
- Code an AI tool reproduces from another project keeps that project's license. Submit it only under a license
  compatible with the GNU Affero General Public License v3.0, and name the project in the pull request.
- Code ported from SimulationCraft names the SimulationCraft file it is from.

## Credentials and private data

FellowshipLogs API clients, tokens and the contents of `settings.json` stay on your machine. Keep them out of prompts,
commits, issues and pull requests.

## Disclosure

Disclosure of AI use is optional. Every contribution has the same review and the same requirements.
# `fennec feeds`

Manages the NuGet feed sources Fennec.Labs uses when resolving packages for `instrument`,
`compare`, and `reproduce`. Feeds are stored in the tool's own configuration, independent of any
project's `nuget.config`.

## When to use it

- Adding a private/internal feed so `--nuget` lookups can resolve packages not on nuget.org.
- Setting a default feed for a specific workflow (e.g. an internal mirror).
- Reviewing which feeds are currently configured before troubleshooting a package resolution issue.

## Subcommands

### `feeds list`

Lists all configured feeds. No options besides the [global options](../README.md#global-options).

```bash
fennec feeds list
fennec feeds list --json
```

### `feeds add`

| Option | Short | Required | Description |
|--------|-------|----------|-------------|
| `--name <name>` | `-n` | yes | Name to register the feed under. |
| `--source <url>` | `-s` | yes | Feed source URL (NuGet v3 index endpoint). |
| `--default` | `-d` | no | Mark this feed as the default used for resolution. |

```bash
fennec feeds add --name MyFeed --source https://my.feed/v3/index.json
fennec feeds add --name MyFeed --source https://my.feed/v3/index.json --default
```

### `feeds remove`

| Option | Short | Required | Description |
|--------|-------|----------|-------------|
| `--name <name>` | `-n` | yes | Name of the feed to remove. |

```bash
fennec feeds remove --name MyFeed
```

## Output

All three subcommands print a status line (human) or a `{ "status": "..." }` JSON object
(`--json`); nothing is written under `.fennec/`.

## Edge cases & troubleshooting

- `feeds remove` with an unknown name → `Error: <message>`, exits 1.
- `feeds add`/`feeds remove` require both `--name`/`--source` up front — System.CommandLine
  enforces these before the handler runs and prints command help on a missing value.

import { fileURLToPath } from "node:url";

// Set tool configuration before vinext imports Wrangler. Using JavaScript here
// works with Windows cmd.exe as well as POSIX shells, without another package.
process.env.WRANGLER_LOG_PATH ??= ".wrangler/wrangler.log";

// Run the installed CLI in this process so arguments, signals, and exit codes
// retain their usual behavior. The pinned vinext package ships cli.js beside
// its exported main entry point in dist/.
const cliUrl = new URL("./cli.js", import.meta.resolve("vinext"));
process.argv[1] = fileURLToPath(cliUrl);
await import(cliUrl.href);

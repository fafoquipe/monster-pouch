#!/usr/bin/env node
// Offline analysis only. Writes JSON to stdout; never opens Unity or player files.
const defaults = {
  targets: [150, 300], winRates: [0.4, 0.5, 0.6, 0.7, 1],
  floor: 0, start: 0, royalStart: 150, win: 30, classicLoss: 5, royalLoss: 30,
  prepSeconds: 40, combatSeconds: 40,
  normalTransitionSeconds: 3.5, hpTransitionSeconds: 7,
  normalFinalSeconds: 3, hpFinalSeconds: 6.5,
  minRounds: 3, maxRounds: 5,
};
const names = {
  '--targets': 'targets', '--win-rates': 'winRates', '--floor': 'floor', '--start': 'start',
  '--royal-start': 'royalStart', '--win': 'win', '--classic-loss': 'classicLoss', '--royal-loss': 'royalLoss',
  '--prep-seconds': 'prepSeconds', '--combat-seconds': 'combatSeconds',
  '--normal-transition-seconds': 'normalTransitionSeconds', '--hp-transition-seconds': 'hpTransitionSeconds',
  '--normal-final-seconds': 'normalFinalSeconds', '--hp-final-seconds': 'hpFinalSeconds',
  '--min-rounds': 'minRounds', '--max-rounds': 'maxRounds',
};
const limits = { maxStates: 400, maxScore: 1000000, maxStep: 10000, maxCases: 100, maxSeconds: 3600 };
const config = { ...defaults };
function fail(message) { throw new Error(message); }
function number(text) {
  if (typeof text !== 'string' || !/^-?(?:\d+(?:\.\d*)?|\.\d+)$/.test(text)) fail(`Invalid number: ${text}`);
  const value = Number(text);
  if (!Number.isFinite(value)) fail(`Non-finite number: ${text}`);
  return value;
}
function integer(value, min, max, name) {
  if (!Number.isSafeInteger(value) || value < min || value > max) fail(`${name} must be an integer in [${min}, ${max}].`);
}
function gcd(a, b) { while (b) [a, b] = [b, a % b]; return a; }
function duration(rounds, transition, final) {
  return rounds * (config.prepSeconds + config.combatSeconds) + (rounds - 1) * transition + final;
}
function expectedMatches(target, p) {
  if (config.start >= target) return { status: 'already_reached', expectedBo5: 0, states: 0 };
  const step = gcd(gcd(config.win, config.classicLoss), gcd(config.royalLoss, config.start - config.floor));
  const count = Math.ceil((target - config.floor) / step);
  if (count > limits.maxStates) fail(`Target ${target} needs ${count} states; limit is ${limits.maxStates}.`);
  if (p === 0) return { status: 'unreachable_without_wins', expectedBo5: null, states: count };
  const winTo = new Int32Array(count), loseTo = new Int32Array(count);
  const matrix = Array.from({ length: count }, () => new Float64Array(count + 1));
  for (let i = 0; i < count; i++) {
    const score = config.floor + i * step;
    const loss = score < config.royalStart ? config.classicLoss : config.royalLoss;
    winTo[i] = i + config.win / step;
    loseTo[i] = Math.max(0, i - loss / step);
    matrix[i][i] = 1;
    if (winTo[i] < count) matrix[i][winTo[i]] -= p;
    matrix[i][loseTo[i]] -= 1 - p;
    matrix[i][count] = 1;
  }
  // Solve (I - Q) E = 1 with partial pivoting. Reject unstable results rather than inventing a duration.
  for (let c = 0; c < count; c++) {
    let pivotRow = c;
    for (let r = c + 1; r < count; r++) if (Math.abs(matrix[r][c]) > Math.abs(matrix[pivotRow][c])) pivotRow = r;
    if (Math.abs(matrix[pivotRow][c]) < 1e-12) return { status: 'numerically_ill_conditioned', expectedBo5: null, states: count };
    [matrix[c], matrix[pivotRow]] = [matrix[pivotRow], matrix[c]];
    const pivot = matrix[c][c];
    for (let j = c; j <= count; j++) matrix[c][j] /= pivot;
    for (let r = c + 1; r < count; r++) {
      const factor = matrix[r][c];
      if (factor === 0) continue;
      matrix[r][c] = 0;
      for (let j = c + 1; j <= count; j++) matrix[r][j] -= factor * matrix[c][j];
    }
  }
  const expected = new Float64Array(count);
  for (let i = count - 1; i >= 0; i--) {
    let value = matrix[i][count];
    for (let j = i + 1; j < count; j++) value -= matrix[i][j] * expected[j];
    expected[i] = value;
  }
  let residual = 0;
  for (let i = 0; i < count; i++) {
    if (!Number.isFinite(expected[i]) || expected[i] < 0) return { status: 'numerically_ill_conditioned', expectedBo5: null, states: count };
    const rhs = 1 + (winTo[i] < count ? p * expected[winTo[i]] : 0) + (1 - p) * expected[loseTo[i]];
    residual = Math.max(residual, Math.abs(expected[i] - rhs) / Math.max(1, Math.abs(rhs)));
  }
  if (residual > 1e-9) return { status: 'numerically_ill_conditioned', expectedBo5: null, states: count, relativeResidual: residual };
  return { status: 'solved', expectedBo5: expected[(config.start - config.floor) / step], states: count, relativeResidual: residual };
}
try {
  const args = process.argv.slice(2);
  if (args.includes('--help')) {
    process.stdout.write('Offline BO5 pacing model. Numeric options use separate values; list options use commas.\n' +
      Object.keys(names).join('\n') + '\nExample: node tools/ladder-pacing.mjs --targets 150,300 --win-rates 0.4,0.5,0.6,0.7,1\n');
    process.exit(0);
  }
  const seen = new Set();
  for (let i = 0; i < args.length; i += 2) {
    const key = names[args[i]];
    if (!key || i + 1 >= args.length || seen.has(key)) fail(`Unknown, repeated or incomplete option: ${args[i]}`);
    seen.add(key);
    config[key] = key === 'targets' || key === 'winRates' ? args[i + 1].split(',').map(number) : number(args[i + 1]);
  }
  for (const key of ['floor', 'start', 'royalStart']) integer(config[key], 0, limits.maxScore, key);
  if (config.start < config.floor || config.royalStart < config.floor) fail('Start and Royal boundary cannot be below the floor.');
  for (const key of ['win', 'classicLoss', 'royalLoss']) integer(config[key], 1, limits.maxStep, key);
  for (const target of config.targets) integer(target, config.floor + 1, limits.maxScore, 'target');
  for (const p of config.winRates) if (!Number.isFinite(p) || p < 0 || p > 1) fail('Win rates must be in [0, 1].');
  if (config.targets.length === 0 || config.winRates.length === 0 || config.targets.length * config.winRates.length > limits.maxCases) fail(`Use between 1 and ${limits.maxCases} target/win-rate cases.`);
  for (const key of Object.keys(config).filter(k => k.endsWith('Seconds'))) {
    if (!Number.isFinite(config[key]) || config[key] < 0 || config[key] > limits.maxSeconds) fail(`${key} must be in [0, ${limits.maxSeconds}].`);
  }
  integer(config.minRounds, 1, 100, 'minRounds'); integer(config.maxRounds, config.minRounds, 100, 'maxRounds');
  const times = {
    normalResultSeconds: [duration(config.minRounds, config.normalTransitionSeconds, config.normalFinalSeconds), duration(config.maxRounds, config.normalTransitionSeconds, config.normalFinalSeconds)],
    healthTiebreakResultSeconds: [duration(config.minRounds, config.hpTransitionSeconds, config.hpFinalSeconds), duration(config.maxRounds, config.hpTransitionSeconds, config.hpFinalSeconds)],
  };
  const results = config.targets.flatMap(target => config.winRates.map(winRate => {
    const result = expectedMatches(target, winRate);
    return { target, winRate, ...result,
      expectedMinutesAtSpecifiedNormalDurations: result.expectedBo5 === null ? null : times.normalResultSeconds.map(t => t * result.expectedBo5 / 60),
      expectedMinutesAtSpecifiedHpDurations: result.expectedBo5 === null ? null : times.healthTiebreakResultSeconds.map(t => t * result.expectedBo5 / 60),
    };
  }));
  process.stdout.write(JSON.stringify({ model: 'finite absorbing Markov chain; constant independent BO5 win probability; pre-match score selects loss band; floor clamps losses',
    scope: 'Offline analytical scenarios, not player measurements or guaranteed times. Defaults match the approved local test; parameter overrides are hypothetical.',
    exclusions: ['drawn rounds', 'pauses', 'network/loading delays', 'time spent on final screen', 'skill or win-rate changes'],
    config, limits, times, results }, null, 2) + '\n');
} catch (error) { process.stderr.write(`ladder-pacing: ${error.message}\n`); process.exitCode = 1; }

#!/usr/bin/env node

import { performance } from 'node:perf_hooks';

const baseUrl = (process.env.BSHOP_STORE_API ?? 'http://localhost/BShopApi/api/online-store').replace(/\/$/, '');
const virtualUsers = Math.max(1, Number.parseInt(process.env.BSHOP_VUS ?? '1', 10));
const durationSeconds = Math.max(5, Number.parseInt(process.env.BSHOP_DURATION_SECONDS ?? '30', 10));
const requestCap = Math.max(1, Number.parseFloat(process.env.BSHOP_REQUESTS_PER_SECOND ?? '5'));
const timeoutMs = Math.max(1000, Number.parseInt(process.env.BSHOP_TIMEOUT_MS ?? '5000', 10));
const outputPath = process.env.BSHOP_RESULT_FILE;

const results = [];
let stopRequested = false;

function percentile(values, p) {
  if (values.length === 0) return null;
  const ordered = [...values].sort((a, b) => a - b);
  return ordered[Math.min(ordered.length - 1, Math.ceil((p / 100) * ordered.length) - 1)];
}

async function timedGet(path, journey) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  const started = performance.now();
  try {
    const response = await fetch(`${baseUrl}${path}`, {
      headers: { Accept: 'application/json', 'User-Agent': 'BShop-Readiness-Load/1.0' },
      signal: controller.signal
    });
    const elapsedMs = performance.now() - started;
    results.push({ journey, status: response.status, elapsedMs, ok: response.ok });
    await response.arrayBuffer();
    if (response.status >= 500) stopRequested = true;
    return response;
  } catch (error) {
    results.push({ journey, status: 0, elapsedMs: performance.now() - started, ok: false, error: error.name });
    return null;
  } finally {
    clearTimeout(timer);
  }
}

async function discoverProductId() {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  const started = performance.now();
  let response;
  try {
    response = await fetch(`${baseUrl}/products?page=1&pageSize=12`, {
      headers: { Accept: 'application/json', 'User-Agent': 'BShop-Readiness-Load/1.0' },
      signal: controller.signal
    });
  } catch (error) {
    results.push({ journey: 'catalog-warmup', status: 0, elapsedMs: performance.now() - started, ok: false, error: error.name });
    return null;
  } finally {
    clearTimeout(timer);
  }
  results.push({ journey: 'catalog-warmup', status: response.status, elapsedMs: performance.now() - started, ok: response.ok });
  if (!response.ok) return null;
  const payload = await response.json().catch(() => null);
  const candidates = payload?.data?.items ?? payload?.data ?? payload?.items ?? [];
  return Array.isArray(candidates) && candidates.length > 0 ? (candidates[0].id ?? candidates[0].productId) : null;
}

async function worker(workerNumber, deadline, productId) {
  let iteration = workerNumber;
  const intervalMs = Math.ceil((1000 * virtualUsers) / requestCap);
  while (performance.now() < deadline && !stopRequested) {
    const choice = iteration % 20;
    if (choice < 12) {
      await timedGet(`/products?page=${(iteration % 3) + 1}&pageSize=12`, 'browse');
    } else if (choice < 16) {
      await timedGet('/products?page=1&pageSize=12&search=Solar', 'search');
    } else if (choice < 19 && productId) {
      await timedGet(`/products/${encodeURIComponent(productId)}`, 'detail');
    } else {
      await timedGet('/policies', 'checkout-read');
    }
    iteration += virtualUsers;
    await new Promise(resolve => setTimeout(resolve, intervalMs));
  }
}

const testStarted = new Date();
const productId = await discoverProductId();
const deadline = performance.now() + durationSeconds * 1000;
await Promise.all(Array.from({ length: virtualUsers }, (_, index) => worker(index, deadline, productId)));
const testEnded = new Date();

const measured = results.filter(item => item.journey !== 'catalog-warmup');
const latencies = measured.map(item => item.elapsedMs);
const statusCounts = Object.fromEntries(
  [...new Set(measured.map(item => item.status))].sort((a, b) => a - b).map(status => [status, measured.filter(item => item.status === status).length])
);
const successful = measured.filter(item => item.ok).length;
const elapsedSeconds = Math.max(0.001, (testEnded - testStarted) / 1000);
const summary = {
  baseUrl,
  startedUtc: testStarted.toISOString(),
  endedUtc: testEnded.toISOString(),
  virtualUsers,
  configuredDurationSeconds: durationSeconds,
  requestRateCap: requestCap,
  requests: measured.length,
  successful,
  successRate: measured.length ? successful / measured.length : 0,
  requestsPerSecond: measured.length / elapsedSeconds,
  latencyMs: {
    average: latencies.length ? latencies.reduce((sum, value) => sum + value, 0) / latencies.length : null,
    p50: percentile(latencies, 50),
    p95: percentile(latencies, 95),
    p99: percentile(latencies, 99)
  },
  statusCounts,
  stoppedOnServerError: stopRequested,
  journeys: Object.fromEntries(
    ['browse', 'search', 'detail', 'checkout-read'].map(journey => {
      const subset = measured.filter(item => item.journey === journey);
      return [journey, { requests: subset.length, successful: subset.filter(item => item.ok).length, p95Ms: percentile(subset.map(item => item.elapsedMs), 95) }];
    })
  )
};

const serialized = `${JSON.stringify(summary, null, 2)}\n`;
if (outputPath) {
  const { writeFile } = await import('node:fs/promises');
  await writeFile(outputPath, serialized, 'utf8');
}
process.stdout.write(serialized);

if (summary.stoppedOnServerError || summary.successRate < 0.99) process.exitCode = 2;

// Some components hold process-wide static state across a request (e.g. the
// StripeWebhookHandler idempotency cache), and several test classes exercise them.
// Running test collections in parallel lets those classes race on the shared state,
// which makes a handful of tests (notably the webhook idempotency test) flaky.
// Disabling collection parallelization keeps the suite deterministic.
//
// If suite runtime becomes a concern as the template grows, re-enable parallelism and
// instead isolate the shared-state tests into a dedicated xUnit [Collection].
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

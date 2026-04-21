/**
 * QA helper — displays the Restore operation blade in an error state
 * with a long file-path error message, to verify word-wrap in .op-card__error.
 *
 * USAGE:
 *   1. Open https://localhost:5001/#!/workspace/embedded-app/system-operations
 *   2. Open browser DevTools (F12) → Console tab
 *   3. Switch the console context to the iframe:
 *      top bar of the Console panel → dropdown labeled "top" → pick the
 *      iframe whose URL contains "/apps/system-operations/"
 *   4. Paste this entire file into the console and press Enter
 *   5. The "Import Platform Data" card will:
 *        - auto-fill with a fake manifest (skipping the drag-drop step)
 *        - let you click "Start Import"
 *        - show progress for ~2 seconds
 *        - then render the error state with a long-path message
 *
 *   To stop mocking and restore normal behavior, run: __stopRestoreMock()
 */

(function () {
  if (window.__restoreMockActive) {
    console.warn('[demo-restore-error] Mock already active. Run __stopRestoreMock() first.');
    return;
  }

  const originalFetch = window.fetch.bind(window);

  const FAKE_ASSET_URL = 'vc_backup_20260420140043.zip';
  const FAKE_NOTIFICATION_ID = 'demo-restore-error-' + Date.now();
  const LONG_ERROR = "Could not find a part of the path '/opt/virtocommerce/platform/app_data/uploads/vc_backup_20260420140043.zip'";

  const FAKE_MANIFEST = {
    author: 'QA Demo',
    platformVersion: '3.1000.0',
    created: new Date().toISOString(),
    handleSecurity: true,
    handleSettings: true,
    handleDynamicProperties: true,
    handleBinaryData: true,
    modules: [
      { id: 'VirtoCommerce.Core', version: '3.1000.0', isChecked: true },
      { id: 'VirtoCommerce.Catalog', version: '3.1000.0', isChecked: true },
    ],
  };

  let pollCount = 0;

  function json(data, status = 200) {
    return new Response(JSON.stringify(data), {
      status,
      headers: { 'Content-Type': 'application/json' },
    });
  }

  window.fetch = async function mockedFetch(input, init) {
    const url = typeof input === 'string' ? input : input.url;
    const method = (init?.method || (typeof input !== 'string' ? input.method : 'GET') || 'GET').toUpperCase();

    // Mock 1: file upload → return a fake asset
    if (url.includes('/api/assets/localstorage') && method === 'POST') {
      console.log('[demo-restore-error] mocked upload →', FAKE_ASSET_URL);
      return json([{ url: FAKE_ASSET_URL, name: FAKE_ASSET_URL, size: 12345 }]);
    }

    // Mock 2: load manifest from uploaded file
    if (url.includes('/api/platform/export/manifest/load')) {
      console.log('[demo-restore-error] mocked manifest load');
      return json(FAKE_MANIFEST);
    }

    // Mock 3: start import → return fake notification
    if (url.includes('/api/platform/import') && method === 'POST') {
      console.log('[demo-restore-error] mocked import start');
      pollCount = 0;
      return json({
        id: FAKE_NOTIFICATION_ID,
        jobId: 'demo-job-' + Date.now(),
        title: 'Platform import',
        description: 'Starting import...',
        created: new Date().toISOString(),
        finished: null,
        totalCount: 100,
        processedCount: 0,
        errorCount: 0,
        errors: [],
      });
    }

    // Mock 4: push notifications polling
    if (url.includes('/api/platform/pushnotifications') && method === 'POST') {
      pollCount += 1;
      // First poll: still running at 50%
      if (pollCount === 1) {
        console.log('[demo-restore-error] mocked poll #1 (in progress)');
        return json({
          totalCount: 1,
          newCount: 0,
          notifyEvents: [{
            id: FAKE_NOTIFICATION_ID,
            description: 'Processing modules...',
            totalCount: 100,
            processedCount: 50,
            finished: null,
            errorCount: 0,
            errors: [],
          }],
        });
      }
      // Second poll onwards: finished with error
      console.log('[demo-restore-error] mocked poll #' + pollCount + ' (finished with error)');
      return json({
        totalCount: 1,
        newCount: 0,
        notifyEvents: [{
          id: FAKE_NOTIFICATION_ID,
          description: 'Platform restore process completed successfully.',
          totalCount: 100,
          processedCount: 100,
          finished: new Date().toISOString(),
          errorCount: 1,
          errors: [LONG_ERROR],
        }],
      });
    }

    // Everything else: pass through to the real server
    return originalFetch(input, init);
  };

  window.__restoreMockActive = true;

  window.__stopRestoreMock = function () {
    window.fetch = originalFetch;
    delete window.__restoreMockActive;
    delete window.__stopRestoreMock;
    console.log('[demo-restore-error] Mock stopped. Normal fetch restored.');
  };

  console.log(
    '%c[demo-restore-error] Mock active.',
    'color:#43b0e6;font-weight:bold;',
    '\nNow do this in the UI:',
    '\n  1. Scroll to "Import Platform Data" card',
    '\n  2. Drop ANY file (even a tiny .zip) on the dropzone, or click it and pick one',
    '\n  3. The manifest auto-loads (mocked)',
    '\n  4. Click "Start Import"',
    '\n  5. After ~2 seconds the error state appears with a long path',
    '\n\nRun __stopRestoreMock() to disable.'
  );
})();

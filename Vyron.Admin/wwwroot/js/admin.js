(function () {
    'use strict';

    function $(sel) { return document.querySelector(sel); }
    function $$(sel) { return document.querySelectorAll(sel); }

    // Hamburger drawer toggle
    var hamburger = $('.hamburger');
    var sidebar = $('.sidebar');
    var backdrop = $('.sidebar-backdrop');

    function openDrawer() {
        if (sidebar) sidebar.classList.add('open');
        if (backdrop) backdrop.classList.add('show');
        if (hamburger) hamburger.setAttribute('aria-expanded', 'true');
    }
    function closeDrawer() {
        if (sidebar) sidebar.classList.remove('open');
        if (backdrop) backdrop.classList.remove('show');
        if (hamburger) hamburger.setAttribute('aria-expanded', 'false');
    }

    if (hamburger) hamburger.addEventListener('click', function () {
        if (sidebar && sidebar.classList.contains('open')) closeDrawer();
        else openDrawer();
    });
    if (backdrop) backdrop.addEventListener('click', closeDrawer);

    $$('.sidebar-nav a').forEach(function (a) {
        a.addEventListener('click', function () {
            if (window.innerWidth <= 768) closeDrawer();
        });
    });

    // Confirm before destructive actions
    $$('form[data-confirm]').forEach(function (f) {
        f.addEventListener('submit', function (e) {
            if (!confirm(f.dataset.confirm)) e.preventDefault();
        });
    });

    // Auto-dismiss alerts after 5s
    $$('.alert').forEach(function (a) {
        setTimeout(function () { a.style.transition = 'opacity 0.3s'; a.style.opacity = '0'; setTimeout(function () { a.remove(); }, 300); }, 5000);
    });

    // Mobile table card labels
    $$('.table-mobile-card').forEach(function (table) {
        var headers = Array.from(table.querySelectorAll('thead th')).map(function (th) { return th.textContent.trim(); });
        table.querySelectorAll('tbody tr').forEach(function (row) {
            row.querySelectorAll('td').forEach(function (td, i) {
                if (headers[i] && !td.hasAttribute('data-label')) {
                    td.setAttribute('data-label', headers[i]);
                }
            });
        });
    });

    // Image preview + client-side type validation on upload fields.
    $$('input[type="file"]').forEach(function (input) {
        input.addEventListener('change', function () {
            var file = input.files && input.files[0];
            var wrap = input.closest('.form-group') || input.parentElement || input;
            var oldError = wrap.querySelector('.upload-error');
            var oldPreview = wrap.querySelector('.upload-preview');
            if (oldError) oldError.remove();
            if (!file) {
                if (oldPreview) oldPreview.remove();
                return;
            }

            var name = (file.name || '').toLowerCase();
            var valid = /\.(jpe?g|png|webp)$/.test(name);
            if (!valid) {
                input.value = '';
                if (oldPreview) oldPreview.remove();
                var err = document.createElement('div');
                err.className = 'upload-error';
                err.textContent = 'Please choose a JPG, PNG, or WEBP image.';
                wrap.appendChild(err);
                return;
            }

            var previewId = input.dataset.preview;
            var preview = previewId ? document.getElementById(previewId) : oldPreview;
            if (!preview) {
                preview = document.createElement('img');
                preview.className = 'upload-preview';
                input.insertAdjacentElement('afterend', preview);
            }
            var reader = new FileReader();
            reader.onload = function (e) { preview.src = e.target.result; preview.style.display = 'block'; };
            reader.readAsDataURL(file);
        });
    });

    // Idle session timeout (client-side, configurable from DB via meta tag)
    var idleMeta = document.querySelector('meta[name="idle-timeout"]');
    var idleTimeoutMinutes = idleMeta ? parseInt(idleMeta.content, 10) : 15;
    if (isNaN(idleTimeoutMinutes) || idleTimeoutMinutes < 5) idleTimeoutMinutes = 15;

    // Only apply idle timer on authenticated pages (sidebar is present)
    if (sidebar && idleTimeoutMinutes > 0) {
        var idleTimer;
        var warningTimer;
        var warningShown = false;

        function resetIdleTimer() {
            clearTimeout(idleTimer);
            clearTimeout(warningTimer);
            if (warningShown) {
                var w = document.getElementById('idle-warning');
                if (w) w.remove();
                warningShown = false;
            }
            // Warn 1 minute before timeout
            warningTimer = setTimeout(showIdleWarning, (idleTimeoutMinutes - 1) * 60 * 1000);
            idleTimer = setTimeout(doLogout, idleTimeoutMinutes * 60 * 1000);
        }

        function showIdleWarning() {
            if (warningShown) return;
            warningShown = true;
            var div = document.createElement('div');
            div.id = 'idle-warning';
            div.style.cssText = 'position:fixed;bottom:20px;right:20px;background:#FFB347;color:#0A0A0A;padding:14px 18px;border-radius:10px;z-index:9999;font-weight:700;box-shadow:0 4px 12px rgba(0,0,0,0.2);';
            div.textContent = 'Session will expire in 1 minute due to inactivity.';
            document.body.appendChild(div);
        }

        function doLogout() {
            window.location.href = '/Account/LogoutIdle';
        }

        ['mousemove', 'keydown', 'mousedown', 'touchstart', 'scroll'].forEach(function (evt) {
            document.addEventListener(evt, resetIdleTimer, { passive: true });
        });

        resetIdleTimer();
    }
})();

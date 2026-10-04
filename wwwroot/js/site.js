// Copy buttons: <button data-copy="#someElement">. Used for the one-time password
// banner, where a mis-typed character means the login has to be re-issued.
document.addEventListener('click', function (e) {
    const button = e.target.closest('[data-copy]');

    if (!button) return;

    const source = document.querySelector(button.dataset.copy);

    if (!source) return;

    navigator.clipboard.writeText(source.textContent.trim()).then(function () {
        const original = button.innerHTML;
        button.innerHTML = '<i class="bi bi-check2"></i> Copied';
        setTimeout(function () { button.innerHTML = original; }, 1500);
    });
});

// Roll call: each row posts on its own, so there is no save step to forget. Without JS the
// form still works, it just reloads the page.
document.addEventListener('submit', function (e) {
    const form = e.target.closest('.tsc-roll-form');

    if (!form) return;

    e.preventDefault();

    const row = form.closest('.tsc-roll-row');
    const button = form.querySelector('button');

    button.disabled = true;

    fetch(form.action, {
        method: 'POST',
        body: new FormData(form),
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
    })
        .then(function (response) {
            if (!response.ok) throw new Error(response.status);
            return response.json();
        })
        .then(function (data) {
            row.classList.toggle('is-present', data.present);
            button.classList.toggle('btn-success', data.present);
            button.classList.toggle('btn-outline-secondary', !data.present);
            button.querySelector('i').className = 'bi ' + (data.present ? 'bi-check-lg' : 'bi-dash-lg');
            button.querySelector('span').textContent = data.present ? 'Present' : 'Absent';
            document.getElementById('presentCount').textContent = data.presentCount;
        })
        .catch(function () {
            // A tap that did not save must not look saved.
            form.submit();
        })
        .finally(function () {
            button.disabled = false;
        });
});

// Password reveal. The value sits in data-secret and the element shows dots until asked, so a
// guardian standing at the counter does not read the whole class's passwords off the screen.
function tscSetRevealed(code, revealed) {
    code.textContent = revealed ? code.dataset.secret : '••••••••';
    code.classList.toggle('is-revealed', revealed);

    const button = document.querySelector('[data-reveal="#' + code.id + '"]');

    if (button) {
        button.querySelector('i').className = revealed ? 'bi bi-eye-slash' : 'bi bi-eye';
        button.setAttribute('aria-label', revealed ? 'Hide password' : 'Show password');
    }
}

document.addEventListener('click', function (e) {
    const toggle = e.target.closest('[data-reveal]');

    if (toggle) {
        const code = document.querySelector(toggle.dataset.reveal);
        if (code) tscSetRevealed(code, !code.classList.contains('is-revealed'));
        return;
    }

    // Copy a hidden password without revealing it first.
    const copySecret = e.target.closest('[data-copy-secret]');

    if (copySecret) {
        const code = document.querySelector(copySecret.dataset.copySecret);
        if (!code) return;

        navigator.clipboard.writeText(code.dataset.secret).then(function () {
            const icon = copySecret.querySelector('i');
            icon.className = 'bi bi-check2';
            setTimeout(function () { icon.className = 'bi bi-clipboard'; }, 1500);
        });
        return;
    }

    // Show/hide every password on the logins page at once.
    const all = e.target.closest('#tscRevealAll');

    if (all) {
        const codes = document.querySelectorAll('.tsc-secret');
        const show = !all.classList.contains('is-on');

        codes.forEach(function (code) { tscSetRevealed(code, show); });

        all.classList.toggle('is-on', show);
        all.innerHTML = show
            ? '<i class="bi bi-eye-slash me-1"></i>Hide all passwords'
            : '<i class="bi bi-eye me-1"></i>Show all passwords';
        return;
    }

    // Sign-in page: let someone check what they typed before submitting.
    const peek = e.target.closest('[data-toggle-password]');

    if (peek) {
        const field = document.querySelector(peek.dataset.togglePassword);
        if (!field) return;

        const hidden = field.type === 'password';
        field.type = hidden ? 'text' : 'password';
        peek.querySelector('i').className = hidden ? 'bi bi-eye-slash' : 'bi bi-eye';
        peek.setAttribute('aria-label', hidden ? 'Hide password' : 'Show password');
    }
});

// Navigation drawer. On a phone the sidebar is parked off-screen; the top bar's menu button
// and the bottom bar's "More" both bring it in. Desktop never calls any of this -- CSS pins
// the sidebar open above 992px and hides both buttons.
(function () {
    const sidebar = document.getElementById('tscSidebar');
    const scrim = document.getElementById('tscScrim');
    const menuBtn = document.getElementById('tscMenuBtn');

    if (!sidebar || !scrim) return;

    function setOpen(open) {
        sidebar.classList.toggle('is-open', open);
        scrim.hidden = !open;
        if (menuBtn) menuBtn.setAttribute('aria-expanded', String(open));
        // Stop the page behind the drawer scrolling under the finger.
        document.body.style.overflow = open ? 'hidden' : '';
    }

    document.addEventListener('click', function (e) {
        if (e.target.closest('#tscMenuBtn') || e.target.closest('#tscMoreBtn')) {
            setOpen(!sidebar.classList.contains('is-open'));
            return;
        }

        if (e.target.closest('#tscScrim')) setOpen(false);
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') setOpen(false);
    });

    // Resizing past the breakpoint leaves body overflow hidden otherwise, which locks a
    // desktop page that was last touched on a narrow window.
    window.addEventListener('resize', function () {
        if (window.innerWidth >= 992) setOpen(false);
    });
})();

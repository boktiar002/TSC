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

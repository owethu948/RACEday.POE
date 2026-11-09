/* RaceDay front-end behaviour. Plain JavaScript, no framework. */
(function () {
    'use strict';

    var $ = function (selector, root) { return (root || document).querySelector(selector); };
    var $$ = function (selector, root) { return Array.prototype.slice.call((root || document).querySelectorAll(selector)); };

    // ------------------------------------------------------------------ toast (flash messages from TempData)
    var toast = $('#toast');
    var toastTimer = null;
    function showToast(message, kind) {
        if (!toast || !message) { return; }
        toast.textContent = message;
        toast.className = 'rd-toast ' + (kind || '');
        void toast.offsetWidth;                       // restart the transition
        toast.classList.add('show');
        window.clearTimeout(toastTimer);
        toastTimer = window.setTimeout(function () { toast.classList.remove('show'); }, kind === 'error' ? 7000 : 4000);
    }
    $$('[data-flash]').forEach(function (el) { showToast(el.getAttribute('data-message'), el.getAttribute('data-flash')); });

    // ------------------------------------------------------------------ sidebar (mobile)
    var sidebar = $('#sidebar');
    var backdrop = $('#sidebar-backdrop');
    function setSidebar(open) {
        if (!sidebar) { return; }
        sidebar.classList.toggle('open', open);
        if (backdrop) { backdrop.classList.toggle('open', open); }
    }
    var burger = $('#sidebar-toggle');
    if (burger) { burger.addEventListener('click', function () { setSidebar(!sidebar.classList.contains('open')); }); }
    if (backdrop) { backdrop.addEventListener('click', function () { setSidebar(false); }); }

    // ------------------------------------------------------------------ overlays
    function openOverlay(el) { el.classList.add('open'); el.setAttribute('aria-hidden', 'false'); }
    function closeOverlay(el) { el.classList.remove('open'); el.setAttribute('aria-hidden', 'true'); }
    $$('.modal-overlay').forEach(function (overlay) {
        overlay.addEventListener('click', function (event) {
            if (event.target === overlay || (event.target.closest && event.target.closest('[data-close-modal]'))) { closeOverlay(overlay); }
        });
    });
    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape') {
            $$('.modal-overlay.open').forEach(closeOverlay);
            var panel = $('#notif-panel');
            if (panel) { panel.classList.remove('open'); }
        }
    });

    // ------------------------------------------------------------------ confirm pop-up: <form data-confirm="..." data-confirm-title="..." data-confirm-yes="...">
    var confirmModal = $('#confirm-modal');
    var pendingForm = null;
    document.addEventListener('submit', function (event) {
        var form = event.target;
        if (!form || !form.getAttribute || !form.getAttribute('data-confirm') || form.getAttribute('data-confirmed') === '1') { return; }
        event.preventDefault();

        if (!confirmModal) {                        // no pop-up available: fall back to the browser dialog
            if (window.confirm(form.getAttribute('data-confirm'))) { form.setAttribute('data-confirmed', '1'); form.submit(); }
            return;
        }
        pendingForm = form;
        $('#confirm-title').textContent = form.getAttribute('data-confirm-title') || 'Are you sure?';
        $('#confirm-sub').textContent = form.getAttribute('data-confirm-sub') || 'This action cannot be undone.';
        $('#confirm-message').textContent = form.getAttribute('data-confirm');
        $('#confirm-yes').textContent = form.getAttribute('data-confirm-yes') || 'Yes, do it';
        $('#confirm-no').textContent = form.getAttribute('data-confirm-no') || 'Keep it';
        openOverlay(confirmModal);
    });
    if (confirmModal) {
        $('#confirm-no').addEventListener('click', function () { pendingForm = null; closeOverlay(confirmModal); });
        $('#confirm-yes').addEventListener('click', function () {
            if (pendingForm) { pendingForm.setAttribute('data-confirmed', '1'); pendingForm.submit(); }
            closeOverlay(confirmModal);
        });
    }

    // ------------------------------------------------------------------ enrol pop-up: <button data-enrol-event="12">
    var enrolModal = $('#enrol-modal');
    var enrolContent = $('#enrol-modal-content');
    document.addEventListener('click', function (event) {
        var trigger = event.target.closest && event.target.closest('[data-enrol-event]');
        if (!trigger || !enrolModal) { return; }
        event.preventDefault();
        enrolContent.innerHTML = '<div class="rd-modal-body text-center text-muted">Loading categories…</div>';
        openOverlay(enrolModal);

        fetch('/Events/EnrolModal/' + trigger.getAttribute('data-enrol-event'), { credentials: 'same-origin' })
            .then(function (response) {
                if (response.status === 403) { window.location.href = '/Account/Login'; return null; }
                if (!response.ok) { throw new Error('status ' + response.status); }
                return response.text();
            })
            .then(function (html) { if (html !== null) { enrolContent.innerHTML = html; } })
            .catch(function () {
                closeOverlay(enrolModal);
                showToast("We couldn't load the categories. Please try again.", 'error');
            });
    });
    if (enrolContent) {
        enrolContent.addEventListener('change', function (event) {
            if (event.target.name !== 'categoryId') { return; }
            $$('.cat-opt', enrolContent).forEach(function (label) { label.classList.remove('selected'); });
            event.target.closest('.cat-opt').classList.add('selected');
            var error = $('#enrol-error', enrolContent);
            if (error) { error.hidden = true; }
        });
        enrolContent.addEventListener('submit', function (event) {
            if (!$('input[name="categoryId"]:checked', enrolContent)) {
                event.preventDefault();
                var error = $('#enrol-error', enrolContent);
                if (error) { error.hidden = false; }
            }
        });
    }

    // ------------------------------------------------------------------ notifications bell
    var bell = $('#notif-btn');
    var panel = $('#notif-panel');
    if (bell && panel) {
        bell.addEventListener('click', function (event) {
            event.stopPropagation();
            var open = !panel.classList.contains('open');
            panel.classList.toggle('open', open);
            bell.setAttribute('aria-expanded', String(open));
            if (open) {
                fetch(bell.getAttribute('data-url'), { credentials: 'same-origin' })
                    .then(function (r) { if (!r.ok) { throw new Error(); } return r.text(); })
                    .then(function (html) { $('#notif-body').innerHTML = html; var dot = $('.rd-bell-dot', bell); if (dot) { dot.style.display = 'none'; } })
                    .catch(function () { $('#notif-body').innerHTML = '<div class="notif-item"><span class="notif-txt text-muted">Notifications are unavailable right now.</span></div>'; });
            }
        });
        document.addEventListener('click', function (event) {
            if (!panel.contains(event.target)) { panel.classList.remove('open'); }
        });
    }

    // ------------------------------------------------------------------ image preview: <input type="file" data-preview-target="#img">
    $$('input[type="file"][data-preview-target]').forEach(function (input) {
        input.addEventListener('change', function () {
            var target = $(input.getAttribute('data-preview-target'));
            if (!target) { return; }
            var file = input.files && input.files[0];
            if (file && file.type.indexOf('image/') === 0) { target.src = URL.createObjectURL(file); target.hidden = false; }
            else { target.hidden = true; }
        });
    });

    // ------------------------------------------------------------------ create-event: add / remove category rows
    var rows = $('#cat-rows');
    var addRow = $('#add-cat-row');
    if (rows && addRow) {
        var template = $('#cat-row-template');
        function renumber() {
            $$('.cat-row', rows).forEach(function (row, index) {
                $$('[data-field]', row).forEach(function (input) {
                    input.name = 'Categories[' + index + '].' + input.getAttribute('data-field');
                });
            });
        }
        addRow.addEventListener('click', function () {
            rows.insertAdjacentHTML('beforeend', template.innerHTML);
            renumber();
        });
        rows.addEventListener('click', function (event) {
            var remove = event.target.closest && event.target.closest('.cat-row-remove');
            if (!remove) { return; }
            var all = $$('.cat-row', rows);
            if (all.length > 1) { remove.closest('.cat-row').remove(); }
            else { $$('input', all[0]).forEach(function (i) { i.value = ''; }); }
            renumber();
        });
        renumber();
    }

    // ------------------------------------------------------------------ category page: quick fill + edit modal (Bootstrap modal)
    $$('[data-preset-name]').forEach(function (button) {
        button.addEventListener('click', function () {
            var name = $('#add-name'); var km = $('#add-km');
            if (name) { name.value = button.getAttribute('data-preset-name'); name.focus(); }
            if (km) { km.value = button.getAttribute('data-preset-km'); }
        });
    });
    var editModalElement = $('#editCategoryModal');
    if (editModalElement && window.bootstrap) {
        var editModal = new window.bootstrap.Modal(editModalElement);
        $$('.js-edit-category').forEach(function (button) {
            button.addEventListener('click', function () {
                $('#edit-category-form').setAttribute('action', button.getAttribute('data-edit-url'));
                $('#edit-name').value = button.getAttribute('data-name');
                $('#edit-km').value = button.getAttribute('data-km');
                $('#edit-max').value = button.getAttribute('data-max');
                $('#edit-fee').value = button.getAttribute('data-fee');
                editModal.show();
            });
        });
    }

    // ------------------------------------------------------------------ results entry: time and position only apply to "Finished"
    $$('.js-result-status').forEach(function (select) {
        var row = select.closest('tr');
        if (!row) { return; }
        var time = $('.js-result-time', row);
        var position = $('.js-result-position', row);
        function sync() {
            if (select.disabled) { return; }           // event not concluded yet: the server locked the whole row
            var finished = select.value === 'Finished';
            if (time) { time.disabled = !finished; }
            if (position) { position.disabled = !finished; }
        }
        select.addEventListener('change', sync);
        sync();
    });

    // ------------------------------------------------------------------ register: organiser code only for organisers
    var organiserRadio = $('#role-organiser');
    var codeGroup = $('#organiser-code-group');
    if (organiserRadio && codeGroup) {
        var syncRole = function () { codeGroup.hidden = !organiserRadio.checked; };
        $$('input[name="Role"]').forEach(function (radio) { radio.addEventListener('change', syncRole); });
        syncRole();
    }
})();

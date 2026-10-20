var AdminTechsJs = (function () {

    var ttimer = null;
    var askBox = null, askDone = null, askCheck = null, askBack = null;
    var dirty = false;

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function base() {
        return location.pathname.replace(/[^\/]*$/, '');
    }

    function getCookie(name) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + name + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function qp(name) {
        return new URLSearchParams(location.search).get(name) || '';
    }

    function api(method, list) {
        list = list || [];
        list.push({ key: 'tok', vlu: getCookie('hl_tok') });
        $ApiRequest('AdminTechs/' + method, JSON.stringify(list));
    }

    function expired() {
        document.cookie = 'hl_tok=; path=' + base() + '; max-age=0';
        location.href = 'AdminLogin';
    }

    function signout() {
        api('SignOut');
        setTimeout(expired, 400);
    }

    function toast(msg) {
        var t = el('at-toast');
        t.textContent = msg;
        t.classList.add('at-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('at-open');
        }, 3600);
    }

    function openMenu() {
        el('at-rail').classList.add('at-open');
        el('at-scrim').classList.add('at-open');
    }

    function closeMenu() {
        el('at-rail').classList.remove('at-open');
        el('at-scrim').classList.remove('at-open');
    }

    function openModal(title) {
        var m = el('at-modal');
        el('at-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('at-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('at-modal');
        if (m) {
            m.classList.remove('at-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('at-modal');
        if (!m || !m.classList.contains('at-open')) {
            return;
        }
        if (dirty) {
            ask({ title: 'Leave without saving?', text: 'Your changes will be lost.', ok: 'Leave', no: 'Keep editing', danger: true }, shut);
            return;
        }
        shut();
    }

    function markDirty() {
        dirty = true;
    }

    function clean() {
        dirty = false;
    }

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), i: askBox.querySelector('input'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('at-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('at-open');
        askDone = null;
        askCheck = null;
        if (askBack && askBack.focus && document.body.contains(askBack)) {
            askBack.focus();
        }
        askBack = null;
        if (ok && f) {
            f(v);
        }
    }

    function askOk() {
        var x = askParts();
        var m = askCheck ? askCheck(x.i.value.trim()) : '';
        if (m) {
            x.e.textContent = m;
            x.i.focus();
            return;
        }
        askClose(true);
    }

    function askBuild() {
        askBox = document.createElement('div');
        askBox.className = 'at-ask';
        askBox.innerHTML = '<div class="at-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" aria-label="Answer"><em class="at-err" role="alert"></em><div class="at-askacts"><button type="button" class="at-btn at-btn-ghost"></button><button type="button" class="at-btn"></button></div></div>';
        el('at-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('at-open')) {
                return;
            }
            if (e.key === 'Escape') {
                e.preventDefault();
                e.stopPropagation();
                askClose(false);
            } else if (e.key === 'Enter' && e.target !== x.b[0]) {
                e.preventDefault();
                e.stopPropagation();
                askOk();
            } else if (e.key === 'Tab') {
                var f = [x.i, x.b[0], x.b[1]].filter(function (n) { return n.offsetParent !== null; });
                var at = f.indexOf(document.activeElement);
                e.preventDefault();
                f[(at + (e.shiftKey ? f.length - 1 : 1)) % f.length].focus();
            }
        }, true);
    }

    function ask(o, done) {
        if (!askBox) {
            askBuild();
        }
        var x = askParts(), input = typeof o.value === 'string';
        askBack = document.activeElement;
        askDone = done;
        askCheck = o.check || null;
        x.h.textContent = o.title || '';
        x.p.textContent = o.text || '';
        x.e.textContent = '';
        x.b[0].textContent = o.no || 'Cancel';
        x.b[1].textContent = o.ok || 'OK';
        x.b[1].className = 'at-btn' + (o.danger ? ' at-askdel' : '');
        askBox.firstChild.classList.toggle('at-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('at-open');
        if (input) {
            x.i.focus();
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    function collect(root, extra) {
        var list = extra || [];
        document.querySelectorAll('#' + root + ' [data-k]').forEach(function (n) {
            if (n.type === 'checkbox') {
                list.push({ key: n.getAttribute('data-k'), vlu: n.checked ? '1' : '0' });
            } else if (n.type === 'password') {
                list.push({ key: n.getAttribute('data-k'), vlu: n.value });
            } else {
                list.push({ key: n.getAttribute('data-k'), vlu: (n.value || '').trim() });
            }
        });
        return list;
    }

    var pkTarget = null, pkTimer = null;

    function pkOpen(btn) {
        pkTarget = btn.closest('.at-pic');
        var p = el('at-pk');
        p.classList.add('at-open');
        p.setAttribute('aria-hidden', 'false');
        el('at-pkmsg').textContent = '';
        api('Pick', [{ key: 'pterm', vlu: val('at-pkq') }]);
    }

    function pkClose() {
        var p = el('at-pk');
        p.classList.remove('at-open');
        p.setAttribute('aria-hidden', 'true');
    }

    function pkSearch() {
        clearTimeout(pkTimer);
        pkTimer = setTimeout(function () {
            api('Pick', [{ key: 'pterm', vlu: val('at-pkq') }]);
        }, 250);
    }

    function pkPick(b) {
        if (!pkTarget) {
            return;
        }
        var img = pkTarget.querySelector('img');
        img.src = b.getAttribute('data-thumb');
        img.hidden = false;
        pkTarget.querySelector('input').value = b.getAttribute('data-src');
        markDirty();
        pkClose();
    }

    function pkNone(btn) {
        var w = btn.closest('.at-pic');
        w.querySelector('img').hidden = true;
        w.querySelector('input').value = '';
        markDirty();
    }

    function pkUpload() {
        el('at-pkmsg').textContent = 'Uploading...';
        api('PickUp', [{ key: 'pterm', vlu: '' }]);
    }

    function pkUploaded(msg) {
        var f = el('at-pkfile');
        if (f) {
            f.value = '';
        }
        el('at-pkmsg').textContent = msg;
    }

    function edit(id) {
        api('Edit', [{ key: 'id', vlu: id }]);
    }

    function checkedIn(id) {
        var out = [];
        document.querySelectorAll('#' + id + ' input:checked').forEach(function (n) {
            out.push(n.value);
        });
        return out.join(',');
    }

    function save(id) {
        api('Save', collect('at-form', [{ key: 'id', vlu: id }, { key: 'trades', vlu: checkedIn('at-trades') }, { key: 'days', vlu: checkedIn('at-days') }]));
        return false;
    }

    function saved(msg) {
        shut();
        toast(msg);
    }

    function drop(id, name) {
        ask({ title: 'Delete ' + name + '?', text: 'Only possible for technicians with no jobs on record.', ok: 'Delete', no: 'Keep', danger: true }, function () {
            api('Drop', [{ key: 'id', vlu: id }]);
        });
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                if (askBox && askBox.classList.contains('at-open')) {
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('at-modal');
        if (m) {
            m.addEventListener('input', markDirty);
            m.addEventListener('change', markDirty);
            m.addEventListener('mousedown', function (e) {
                if (e.target === m) {
                    closeModal();
                }
            });
        }
    window.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && el('at-pk').classList.contains('at-open') && !(askBox && askBox.classList.contains('at-open'))) {
            e.stopPropagation();
            pkClose();
        }
    }, true);
        api('Load', [{ key: 'view', vlu: qp('f') }, { key: 'show', vlu: qp('show') }, { key: 'from', vlu: qp('from') }]);
    }

    return {
        reveal: reveal,
        expired: expired,
        signout: signout,
        toast: toast,
        openMenu: openMenu,
        closeMenu: closeMenu,
        openModal: openModal,
        closeModal: closeModal,
        shut: shut,
        clean: clean,
        edit: edit,
        save: save,
        saved: saved,
        drop: drop,
        pkOpen: pkOpen,
        pkClose: pkClose,
        pkSearch: pkSearch,
        pkPick: pkPick,
        pkNone: pkNone,
        pkUpload: pkUpload,
        pkUploaded: pkUploaded
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminTechsJs.reveal);
} else {
    AdminTechsJs.reveal();
}

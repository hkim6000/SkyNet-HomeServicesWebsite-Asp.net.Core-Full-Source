var AdminServicesJs = (function () {

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
        $ApiRequest('AdminServices/' + method, JSON.stringify(list));
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
        var t = el('as-toast');
        t.textContent = msg;
        t.classList.add('as-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('as-open');
        }, 3600);
    }

    function openMenu() {
        el('as-rail').classList.add('as-open');
        el('as-scrim').classList.add('as-open');
    }

    function closeMenu() {
        el('as-rail').classList.remove('as-open');
        el('as-scrim').classList.remove('as-open');
    }

    function openModal(title) {
        var m = el('as-modal');
        el('as-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('as-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('as-modal');
        if (m) {
            m.classList.remove('as-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('as-modal');
        if (!m || !m.classList.contains('as-open')) {
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
        if (!askBox || !askBox.classList.contains('as-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('as-open');
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
        askBox.className = 'as-ask';
        askBox.innerHTML = '<div class="as-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" aria-label="Answer"><em class="as-err" role="alert"></em><div class="as-askacts"><button type="button" class="as-btn as-btn-ghost"></button><button type="button" class="as-btn"></button></div></div>';
        el('as-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('as-open')) {
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
        x.b[1].className = 'as-btn' + (o.danger ? ' as-askdel' : '');
        askBox.firstChild.classList.toggle('as-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('as-open');
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
        pkTarget = btn.closest('.as-pic');
        var p = el('as-pk');
        p.classList.add('as-open');
        p.setAttribute('aria-hidden', 'false');
        el('as-pkmsg').textContent = '';
        api('Pick', [{ key: 'pterm', vlu: val('as-pkq') }]);
    }

    function pkClose() {
        var p = el('as-pk');
        p.classList.remove('as-open');
        p.setAttribute('aria-hidden', 'true');
    }

    function pkSearch() {
        clearTimeout(pkTimer);
        pkTimer = setTimeout(function () {
            api('Pick', [{ key: 'pterm', vlu: val('as-pkq') }]);
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
        var w = btn.closest('.as-pic');
        w.querySelector('img').hidden = true;
        w.querySelector('input').value = '';
        markDirty();
    }

    function pkUpload() {
        el('as-pkmsg').textContent = 'Uploading...';
        api('PickUp', [{ key: 'pterm', vlu: '' }]);
    }

    function pkUploaded(msg) {
        var f = el('as-pkfile');
        if (f) {
            f.value = '';
        }
        el('as-pkmsg').textContent = msg;
    }

    function edit(id) {
        api('Edit', [{ key: 'id', vlu: id }]);
    }

    function save(id) {
        api('Save', collect('as-form', [{ key: 'id', vlu: id }]));
        return false;
    }

    function saved(msg) {
        shut();
        toast(msg);
    }

    function drop(id, name) {
        ask({ title: 'Delete ' + name + '?', text: 'It disappears from the website and the price list.', ok: 'Delete', no: 'Keep', danger: true }, function () {
            api('Drop', [{ key: 'id', vlu: id }]);
        });
    }

    function trade(k) {
        api('TradeForm', [{ key: 'trade', vlu: k }]);
    }

    function saveTrade(k) {
        api('SaveTrade', collect('as-form', [{ key: 'trade', vlu: k }]));
        return false;
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                if (askBox && askBox.classList.contains('as-open')) {
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('as-modal');
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
        if (e.key === 'Escape' && el('as-pk').classList.contains('as-open') && !(askBox && askBox.classList.contains('as-open'))) {
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
        trade: trade,
        saveTrade: saveTrade,
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
    document.addEventListener('DOMContentLoaded', AdminServicesJs.reveal);
} else {
    AdminServicesJs.reveal();
}

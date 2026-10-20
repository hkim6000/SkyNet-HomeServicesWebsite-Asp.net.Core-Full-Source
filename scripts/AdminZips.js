var AdminZipsJs = (function () {

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
        $ApiRequest('AdminZips/' + method, JSON.stringify(list));
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
        var t = el('az-toast');
        t.textContent = msg;
        t.classList.add('az-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('az-open');
        }, 3600);
    }

    function openMenu() {
        el('az-rail').classList.add('az-open');
        el('az-scrim').classList.add('az-open');
    }

    function closeMenu() {
        el('az-rail').classList.remove('az-open');
        el('az-scrim').classList.remove('az-open');
    }

    function openModal(title) {
        var m = el('az-modal');
        el('az-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('az-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('az-modal');
        if (m) {
            m.classList.remove('az-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('az-modal');
        if (!m || !m.classList.contains('az-open')) {
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
        if (!askBox || !askBox.classList.contains('az-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('az-open');
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
        askBox.className = 'az-ask';
        askBox.innerHTML = '<div class="az-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" aria-label="Answer"><em class="az-err" role="alert"></em><div class="az-askacts"><button type="button" class="az-btn az-btn-ghost"></button><button type="button" class="az-btn"></button></div></div>';
        el('az-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('az-open')) {
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
        x.b[1].className = 'az-btn' + (o.danger ? ' az-askdel' : '');
        askBox.firstChild.classList.toggle('az-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('az-open');
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

    var stimer = null;

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            api('List', [{ key: 'term', vlu: val('az-term') }]);
        }, 250);
    }

    function zip(code) {
        api('ZipForm', [{ key: 'code', vlu: code }]);
    }

    function saveZip(code) {
        api('SaveZip', collect('az-form', [{ key: 'code', vlu: code }]));
        return false;
    }

    function dropZip(code) {
        ask({ title: 'Remove ZIP code ' + code + '?', text: 'Visitors from this ZIP code will see that we do not cover it.', ok: 'Remove', no: 'Keep', danger: true }, function () {
            api('DropZip', [{ key: 'code', vlu: code }]);
        });
    }

    function zone(k) {
        api('ZoneForm', [{ key: 'zone', vlu: k }]);
    }

    function saveZone(k) {
        api('SaveZone', collect('az-form', [{ key: 'zone', vlu: k }]));
        return false;
    }

    function dropZone(k) {
        ask({ title: 'Delete this zone?', text: 'Only possible when no ZIP codes use it.', ok: 'Delete zone', no: 'Keep', danger: true }, function () {
            api('DropZone', [{ key: 'zone', vlu: k }]);
        });
    }

    function saved(msg) {
        shut();
        toast(msg);
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                if (askBox && askBox.classList.contains('az-open')) {
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('az-modal');
        if (m) {
            m.addEventListener('input', markDirty);
            m.addEventListener('change', markDirty);
            m.addEventListener('mousedown', function (e) {
                if (e.target === m) {
                    closeModal();
                }
            });
        }
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
        search: search,
        zip: zip,
        saveZip: saveZip,
        dropZip: dropZip,
        zone: zone,
        saveZone: saveZone,
        dropZone: dropZone,
        saved: saved
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminZipsJs.reveal);
} else {
    AdminZipsJs.reveal();
}

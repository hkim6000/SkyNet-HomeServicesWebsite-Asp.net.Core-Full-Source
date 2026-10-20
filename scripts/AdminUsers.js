var AdminUsersJs = (function () {

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
        $ApiRequest('AdminUsers/' + method, JSON.stringify(list));
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
        var t = el('au-toast');
        t.textContent = msg;
        t.classList.add('au-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('au-open');
        }, 3600);
    }

    function openMenu() {
        el('au-rail').classList.add('au-open');
        el('au-scrim').classList.add('au-open');
    }

    function closeMenu() {
        el('au-rail').classList.remove('au-open');
        el('au-scrim').classList.remove('au-open');
    }

    function openModal(title) {
        var m = el('au-modal');
        el('au-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('au-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('au-modal');
        if (m) {
            m.classList.remove('au-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('au-modal');
        if (!m || !m.classList.contains('au-open')) {
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
        if (!askBox || !askBox.classList.contains('au-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('au-open');
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
        askBox.className = 'au-ask';
        askBox.innerHTML = '<div class="au-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" aria-label="Answer"><em class="au-err" role="alert"></em><div class="au-askacts"><button type="button" class="au-btn au-btn-ghost"></button><button type="button" class="au-btn"></button></div></div>';
        el('au-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('au-open')) {
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
        x.b[1].className = 'au-btn' + (o.danger ? ' au-askdel' : '');
        askBox.firstChild.classList.toggle('au-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('au-open');
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

    var presets = {
        tech: { jobs: 'VE', dispatch: 'V' },
        office: { quotes: 'VEX', jobs: 'VEX', dispatch: 'VE', invoices: 'VE', customers: 'V', techs: 'VE', services: 'VE', zips: 'VE', reviews: 'VE', messages: 'VE', media: 'VE' },
        books: { quotes: 'V', jobs: 'V', invoices: 'VEX', customers: 'V' },
        web: { pages: 'VE', media: 'VEX', reviews: 'VEX', messages: 'VE', services: 'VE' }
    };

    function edit(id) {
        api('Edit', [{ key: 'id', vlu: id }]);
    }

    function adminToggle() {
        var on = el('au-f-admin').checked;
        el('au-gridwrap').hidden = on;
        el('au-presets').hidden = on;
    }

    function preset(k) {
        var p = presets[k] || {};
        document.querySelectorAll('#au-rights .au-rr').forEach(function (r) {
            var a = r.getAttribute('data-area');
            var want = p[a] || '';
            var hit = r.querySelector('input[value="' + want + '"]') || r.querySelector('input[value="VE"]') || r.querySelector('input[value="V"]');
            if (!p[a]) {
                hit = r.querySelector('input[value=""]');
            }
            if (hit) {
                hit.checked = true;
            }
        });
        var own = document.querySelector('#au-form [data-k="own"]');
        if (own) {
            own.checked = k === 'tech';
        }
        markDirty();
    }

    function perms() {
        var out = [];
        document.querySelectorAll('#au-rights input[type="radio"]:checked').forEach(function (n) {
            if (n.value) {
                out.push(n.name.replace('au-r-', '') + ':' + n.value);
            }
        });
        return out.join(',');
    }

    function save(id) {
        api('Save', collect('au-form', [{ key: 'id', vlu: id }, { key: 'perms', vlu: perms() }]));
        return false;
    }

    function reset(id, name) {
        ask({ title: 'Reset the password of ' + name + '?', text: 'They get a temporary password and must choose a new one. Their open sessions end.', ok: 'Reset password', no: 'Cancel', danger: true }, function () {
            api('Reset', [{ key: 'id', vlu: id }]);
        });
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                if (askBox && askBox.classList.contains('au-open')) {
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('au-modal');
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
        edit: edit,
        adminToggle: adminToggle,
        preset: preset,
        save: save,
        reset: reset
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminUsersJs.reveal);
} else {
    AdminUsersJs.reveal();
}

var AdminMessagesJs = (function () {

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
        $ApiRequest('AdminMessages/' + method, JSON.stringify(list));
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
        var t = el('ms-toast');
        t.textContent = msg;
        t.classList.add('ms-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ms-open');
        }, 3600);
    }

    function openMenu() {
        el('ms-rail').classList.add('ms-open');
        el('ms-scrim').classList.add('ms-open');
    }

    function closeMenu() {
        el('ms-rail').classList.remove('ms-open');
        el('ms-scrim').classList.remove('ms-open');
    }

    function openModal(title) {
        var m = el('ms-modal');
        el('ms-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('ms-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('ms-modal');
        if (m) {
            m.classList.remove('ms-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('ms-modal');
        if (!m || !m.classList.contains('ms-open')) {
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
        if (!askBox || !askBox.classList.contains('ms-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('ms-open');
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
        askBox.className = 'ms-ask';
        askBox.innerHTML = '<div class="ms-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" aria-label="Answer"><em class="ms-err" role="alert"></em><div class="ms-askacts"><button type="button" class="ms-btn ms-btn-ghost"></button><button type="button" class="ms-btn"></button></div></div>';
        el('ms-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('ms-open')) {
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
        x.b[1].className = 'ms-btn' + (o.danger ? ' ms-askdel' : '');
        askBox.firstChild.classList.toggle('ms-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('ms-open');
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

    var st = 'open', pg = 1, stimer = null;

    function filters(extra) {
        var list = [{ key: 'st', vlu: st }, { key: 'term', vlu: val('ms-term') }, { key: 'pg', vlu: String(pg) }];
        return extra ? list.concat(extra) : list;
    }

    function view(v) {
        st = v;
        pg = 1;
        api('List', filters());
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            pg = 1;
            api('List', filters());
        }, 250);
    }

    function page(n) {
        pg = n;
        api('List', filters());
    }

    function open(id) {
        api('Open', filters([{ key: 'id', vlu: id }]));
    }

    function mark(id, how) {
        api('Mark', filters([{ key: 'id', vlu: id }, { key: 'how', vlu: how }]));
    }

    function drop(id) {
        ask({ title: 'Delete this message?', text: 'It is removed for good.', ok: 'Delete', no: 'Keep', danger: true }, function () {
            api('Drop', filters([{ key: 'id', vlu: id }]));
        });
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                if (askBox && askBox.classList.contains('ms-open')) {
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('ms-modal');
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
        view: view,
        search: search,
        page: page,
        open: open,
        mark: mark,
        drop: drop
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminMessagesJs.reveal);
} else {
    AdminMessagesJs.reveal();
}

var AdminReviewsJs = (function () {

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
        $ApiRequest('AdminReviews/' + method, JSON.stringify(list));
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
        var t = el('ar-toast');
        t.textContent = msg;
        t.classList.add('ar-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ar-open');
        }, 3600);
    }

    function openMenu() {
        el('ar-rail').classList.add('ar-open');
        el('ar-scrim').classList.add('ar-open');
    }

    function closeMenu() {
        el('ar-rail').classList.remove('ar-open');
        el('ar-scrim').classList.remove('ar-open');
    }

    function openModal(title) {
        var m = el('ar-modal');
        el('ar-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('ar-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('ar-modal');
        if (m) {
            m.classList.remove('ar-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('ar-modal');
        if (!m || !m.classList.contains('ar-open')) {
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
        if (!askBox || !askBox.classList.contains('ar-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('ar-open');
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
        askBox.className = 'ar-ask';
        askBox.innerHTML = '<div class="ar-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" aria-label="Answer"><em class="ar-err" role="alert"></em><div class="ar-askacts"><button type="button" class="ar-btn ar-btn-ghost"></button><button type="button" class="ar-btn"></button></div></div>';
        el('ar-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('ar-open')) {
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
        x.b[1].className = 'ar-btn' + (o.danger ? ' ar-askdel' : '');
        askBox.firstChild.classList.toggle('ar-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('ar-open');
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

    function edit(id) {
        api('Edit', [{ key: 'id', vlu: id }]);
    }

    function fromJob(sel) {
        var o = sel.options[sel.selectedIndex];
        if (!o || !o.value) {
            return;
        }
        el('ar-f-name').value = o.getAttribute('data-n');
        el('ar-f-town').value = o.getAttribute('data-t');
        if (o.getAttribute('data-tr')) {
            el('ar-f-trade').value = o.getAttribute('data-tr');
        }
    }

    function save(id) {
        api('Save', collect('ar-form', [{ key: 'id', vlu: id }]));
        return false;
    }

    function saved(msg) {
        shut();
        toast(msg);
    }

    function pub(id, on) {
        if (on) {
            ask({ title: 'Publish this review?', text: 'Only publish feedback the customer agreed to share. It shows on the website right away.', ok: 'Publish', no: 'Not yet' }, function () {
                api('Pub', [{ key: 'id', vlu: id }, { key: 'on', vlu: '1' }]);
            });
        } else {
            api('Pub', [{ key: 'id', vlu: id }, { key: 'on', vlu: '0' }]);
        }
    }

    function drop(id) {
        ask({ title: 'Delete this review?', text: 'It is removed for good.', ok: 'Delete', no: 'Keep', danger: true }, function () {
            api('Drop', [{ key: 'id', vlu: id }]);
        });
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                if (askBox && askBox.classList.contains('ar-open')) {
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('ar-modal');
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
        fromJob: fromJob,
        save: save,
        saved: saved,
        pub: pub,
        drop: drop
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminReviewsJs.reveal);
} else {
    AdminReviewsJs.reveal();
}

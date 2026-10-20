var AdminMediaJs = (function () {

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
        $ApiRequest('AdminMedia/' + method, JSON.stringify(list));
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
        var t = el('am-toast');
        t.textContent = msg;
        t.classList.add('am-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('am-open');
        }, 3600);
    }

    function openMenu() {
        el('am-rail').classList.add('am-open');
        el('am-scrim').classList.add('am-open');
    }

    function closeMenu() {
        el('am-rail').classList.remove('am-open');
        el('am-scrim').classList.remove('am-open');
    }

    function openModal(title) {
        var m = el('am-modal');
        el('am-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('am-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('am-modal');
        if (m) {
            m.classList.remove('am-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('am-modal');
        if (!m || !m.classList.contains('am-open')) {
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
        if (!askBox || !askBox.classList.contains('am-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('am-open');
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
        askBox.className = 'am-ask';
        askBox.innerHTML = '<div class="am-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" aria-label="Answer"><em class="am-err" role="alert"></em><div class="am-askacts"><button type="button" class="am-btn am-btn-ghost"></button><button type="button" class="am-btn"></button></div></div>';
        el('am-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('am-open')) {
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
        x.b[1].className = 'am-btn' + (o.danger ? ' am-askdel' : '');
        askBox.firstChild.classList.toggle('am-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('am-open');
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

    var mk = '';
    var mtimer = null;

    function filters() {
        return [{ key: 'mk', vlu: mk }, { key: 'mq', vlu: val('am-mq') }];
    }

    function lib() {
        api('Lib', filters());
    }

    function libKind(k) {
        mk = k;
        document.querySelectorAll('#am-chips .am-chip').forEach(function (b) {
            b.classList.toggle('am-act', b.getAttribute('data-mk') === k);
        });
        lib();
    }

    function libType() {
        clearTimeout(mtimer);
        mtimer = setTimeout(lib, 250);
    }

    function upload() {
        var msg = el('am-upmsg');
        if (msg) {
            msg.textContent = 'Uploading. Large videos can take a minute.';
        }
        api('Upload', filters());
    }

    function uploaded() {
        document.querySelectorAll('#am-root input[type="file"]').forEach(function (f) {
            f.value = '';
        });
    }

    function opened(title) {
        openModal(title);
    }

    function edit(id) {
        api('Edit', [{ key: 'id', vlu: id }]);
    }

    function save(id) {
        var list = collect('am-form', [{ key: 'id', vlu: id }]);
        list.push.apply(list, filters());
        api('Save', list);
        return false;
    }

    function saved() {
        clean();
        shut();
        toast('Saved');
    }

    function drop(id, name, used) {
        if (used > 0) {
            toast(name + ' is used on the website. Replace it there before deleting it.');
            return;
        }
        ask({ title: 'Delete ' + name + '?', text: 'The file is removed from the library and the server for good.', ok: 'Delete file', no: 'Keep it', danger: true }, function () {
            var list = filters();
            list.push({ key: 'id', vlu: id });
            api('Drop', list);
        });
    }

    function dropped() {
        clean();
        shut();
        toast('File deleted');
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                if (askBox && askBox.classList.contains('am-open')) {
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('am-modal');
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
        libKind: libKind,
        libType: libType,
        upload: upload,
        uploaded: uploaded,
        opened: opened,
        edit: edit,
        save: save,
        saved: saved,
        drop: drop,
        dropped: dropped
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminMediaJs.reveal);
} else {
    AdminMediaJs.reveal();
}

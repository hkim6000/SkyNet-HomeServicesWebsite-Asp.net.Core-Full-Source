var AdminQuotesJs = (function () {

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
        $ApiRequest('AdminQuotes/' + method, JSON.stringify(list));
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
        var t = el('aq-toast');
        t.textContent = msg;
        t.classList.add('aq-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('aq-open');
        }, 3600);
    }

    function openMenu() {
        el('aq-rail').classList.add('aq-open');
        el('aq-scrim').classList.add('aq-open');
    }

    function closeMenu() {
        el('aq-rail').classList.remove('aq-open');
        el('aq-scrim').classList.remove('aq-open');
    }

    function openModal(title) {
        var m = el('aq-modal');
        el('aq-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('aq-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('aq-modal');
        if (m) {
            m.classList.remove('aq-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('aq-modal');
        if (!m || !m.classList.contains('aq-open')) {
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
        if (!askBox || !askBox.classList.contains('aq-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('aq-open');
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
        askBox.className = 'aq-ask';
        askBox.innerHTML = '<div class="aq-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" aria-label="Answer"><em class="aq-err" role="alert"></em><div class="aq-askacts"><button type="button" class="aq-btn aq-btn-ghost"></button><button type="button" class="aq-btn"></button></div></div>';
        el('aq-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('aq-open')) {
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
        x.b[1].className = 'aq-btn' + (o.danger ? ' aq-askdel' : '');
        askBox.firstChild.classList.toggle('aq-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('aq-open');
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

    var st = 'open', pg = 1, stimer = null, ctimer = null;

    function filters(extra) {
        var list = [{ key: 'st', vlu: st }, { key: 'term', vlu: val('aq-term') }, { key: 'pg', vlu: String(pg) }];
        return extra ? list.concat(extra) : list;
    }

    function loaded(v) {
        st = v;
    }

    function view(v) {
        st = v;
        pg = 1;
        history.replaceState(null, '', 'AdminQuotes' + (v !== 'open' ? '?f=' + v : ''));
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

    function edit(id) {
        api('Edit', [{ key: 'id', vlu: id }]);
    }

    function fixLink() {
        var l = el('aq-link');
        if (!l) {
            return;
        }
        var url = location.origin + base() + 'QuoteView?k=' + l.getAttribute('data-key');
        l.value = url;
        var m = el('aq-mail');
        if (m) {
            m.value = m.value.replace('{link}', url);
            var a = el('aq-mailto');
            if (a) {
                a.href = 'mailto:' + a.getAttribute('data-to') + '?subject=' + encodeURIComponent(a.getAttribute('data-sub')) + '&body=' + encodeURIComponent(m.value);
            }
        }
    }

    function opened(title) {
        var m = el('aq-modal');
        if (!m.classList.contains('aq-open')) {
            openModal(title);
        } else {
            el('aq-mtitle').textContent = title;
        }
        fixLink();
    }

    function add() {
        api('AddForm');
    }

    function create() {
        var svc = [];
        document.querySelectorAll('input[name="aq-nsvc"]:checked').forEach(function (n) {
            svc.push(n.value);
        });
        api('Create', filters(collect('aq-nform', [{ key: 'services', vlu: svc.join(',') }])));
        return false;
    }

    function lines() {
        var out = [];
        document.querySelectorAll('#aq-lines .aq-ln').forEach(function (r) {
            out.push({ t: r.querySelector('[data-f="t"]').value.trim(), q: r.querySelector('[data-f="q"]').value.trim(), p: r.querySelector('[data-f="p"]').value.trim() });
        });
        return JSON.stringify(out);
    }

    function calc() {
        clearTimeout(ctimer);
        ctimer = setTimeout(function () {
            api('Calc', [{ key: 'lines', vlu: lines() }, { key: 'trip', vlu: val('aq-trip') }]);
        }, 300);
    }

    function addLine(text, price) {
        var t = el('aq-lntpl');
        if (!t) {
            return;
        }
        var row = t.content.firstElementChild.cloneNode(true);
        el('aq-lines').appendChild(row);
        if (text) {
            row.querySelector('[data-f="t"]').value = text;
            row.querySelector('[data-f="p"]').value = price;
        }
        row.querySelector('[data-f="t"]').focus();
        markDirty();
        calc();
    }

    function addService(sel) {
        var o = sel.options[sel.selectedIndex];
        if (o && o.value) {
            addLine(o.getAttribute('data-n'), o.getAttribute('data-p'));
        }
        sel.value = '';
    }

    function dropLine(btn) {
        btn.closest('.aq-ln').remove();
        markDirty();
        calc();
    }

    function save(send) {
        var go = function () {
            var list = collect('aq-form', [{ key: 'id', vlu: el('aq-form').getAttribute('data-id') }, { key: 'lines', vlu: lines() }, { key: 'trip', vlu: val('aq-trip') }, { key: 'note', vlu: val('aq-note') }, { key: 'snote', vlu: val('aq-snote') }, { key: 'send', vlu: send ? '1' : '0' }]);
            api('Save', filters(list));
        };
        if (send) {
            ask({ title: 'Send this quote?', text: 'The quote gets a link the customer can open and accept. You can withdraw it later.', ok: 'Send quote', no: 'Not yet' }, go);
        } else {
            go();
        }
    }

    function saved(msg) {
        clean();
        toast(msg);
        fixLink();
    }

    function saveNote() {
        api('SaveNote', [{ key: 'id', vlu: el('aq-form').getAttribute('data-id') }, { key: 'snote', vlu: val('aq-snote') }]);
    }

    function act(a, id) {
        var send = function (why) {
            api('Act', filters([{ key: 'id', vlu: id }, { key: 'act', vlu: a }, { key: 'why', vlu: why || '' }]));
        };
        if (a === 'close') {
            ask({ title: 'Close this request?', text: 'Use this for spam, duplicates or jobs we will not take. You can reopen it later.', value: '', place: 'Reason (optional)', ok: 'Close request', no: 'Keep it', danger: true }, send);
        } else if (a === 'withdraw') {
            ask({ title: 'Withdraw this quote?', text: 'The customer can no longer accept it until you send it again.', ok: 'Withdraw', no: 'Keep it sent', danger: true }, send);
        } else if (a === 'phone') {
            ask({ title: 'Customer accepted by phone?', text: 'Type the name of the person who said yes.', value: '', place: 'Name', ok: 'Mark accepted', no: 'Cancel', check: function (v) { return v.length < 2 ? 'Type the name.' : ''; } }, send);
        } else {
            ask({ title: 'Reopen as a draft?', text: 'You can then change the lines and send it again.', ok: 'Reopen', no: 'Cancel' }, send);
        }
    }

    function copy(id) {
        var n = el(id);
        n.select();
        try {
            navigator.clipboard.writeText(n.value).then(function () { toast('Copied'); }, function () { document.execCommand('copy'); toast('Copied'); });
        } catch (e) {
            document.execCommand('copy');
            toast('Copied');
        }
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                if (askBox && askBox.classList.contains('aq-open')) {
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('aq-modal');
        if (m) {
            m.addEventListener('input', markDirty);
            m.addEventListener('change', markDirty);
            m.addEventListener('mousedown', function (e) {
                if (e.target === m) {
                    closeModal();
                }
            });
        }
        document.addEventListener('input', function (e) {
            if (e.target.closest && (e.target.closest('#aq-lines') || e.target.id === 'aq-trip')) {
                calc();
            }
        });
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
        loaded: loaded,
        view: view,
        search: search,
        page: page,
        edit: edit,
        opened: opened,
        add: add,
        create: create,
        addLine: addLine,
        addService: addService,
        dropLine: dropLine,
        save: save,
        saved: saved,
        saveNote: saveNote,
        act: act,
        copy: copy
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminQuotesJs.reveal);
} else {
    AdminQuotesJs.reveal();
}

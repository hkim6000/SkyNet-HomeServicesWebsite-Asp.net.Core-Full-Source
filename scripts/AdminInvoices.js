var AdminInvoicesJs = (function () {

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
        $ApiRequest('AdminInvoices/' + method, JSON.stringify(list));
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
        var t = el('ai-toast');
        t.textContent = msg;
        t.classList.add('ai-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ai-open');
        }, 3600);
    }

    function openMenu() {
        el('ai-rail').classList.add('ai-open');
        el('ai-scrim').classList.add('ai-open');
    }

    function closeMenu() {
        el('ai-rail').classList.remove('ai-open');
        el('ai-scrim').classList.remove('ai-open');
    }

    function openModal(title) {
        var m = el('ai-modal');
        el('ai-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('ai-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('ai-modal');
        if (m) {
            m.classList.remove('ai-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('ai-modal');
        if (!m || !m.classList.contains('ai-open')) {
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
        if (!askBox || !askBox.classList.contains('ai-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('ai-open');
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
        askBox.className = 'ai-ask';
        askBox.innerHTML = '<div class="ai-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" aria-label="Answer"><em class="ai-err" role="alert"></em><div class="ai-askacts"><button type="button" class="ai-btn ai-btn-ghost"></button><button type="button" class="ai-btn"></button></div></div>';
        el('ai-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('ai-open')) {
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
        x.b[1].className = 'ai-btn' + (o.danger ? ' ai-askdel' : '');
        askBox.firstChild.classList.toggle('ai-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('ai-open');
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

    var st = 'unpaid', pg = 1, stimer = null, ctimer = null;

    function filters(extra) {
        var list = [{ key: 'st', vlu: st }, { key: 'term', vlu: val('ai-term') }, { key: 'pg', vlu: String(pg) }];
        return extra ? list.concat(extra) : list;
    }

    function loaded(v) {
        st = v;
    }

    function view(v) {
        st = v;
        pg = 1;
        history.replaceState(null, '', 'AdminInvoices?f=' + v);
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

    function opened(title) {
        var m = el('ai-modal');
        if (!m.classList.contains('ai-open')) {
            openModal(title);
        } else {
            el('ai-mtitle').textContent = title;
        }
    }

    function make(job) {
        api('Make', filters([{ key: 'job', vlu: job }]));
    }

    function lines() {
        var out = [];
        document.querySelectorAll('#ai-lines .ai-ln').forEach(function (r) {
            out.push({ t: r.querySelector('[data-f="t"]').value.trim(), q: r.querySelector('[data-f="q"]').value.trim(), p: r.querySelector('[data-f="p"]').value.trim() });
        });
        return JSON.stringify(out);
    }

    function calc() {
        clearTimeout(ctimer);
        ctimer = setTimeout(function () {
            var f = el('ai-form');
            api('Calc', [{ key: 'id', vlu: f ? f.getAttribute('data-id') : '' }, { key: 'lines', vlu: lines() }, { key: 'trip', vlu: val('ai-trip') }]);
        }, 300);
    }

    function addLine() {
        var row = el('ai-lntpl').content.firstElementChild.cloneNode(true);
        el('ai-lines').appendChild(row);
        row.querySelector('[data-f="t"]').focus();
        markDirty();
    }

    function dropLine(btn) {
        btn.closest('.ai-ln').remove();
        markDirty();
        calc();
    }

    function save(id, issue) {
        var go = function () {
            api('Save', filters([{ key: 'id', vlu: id }, { key: 'lines', vlu: lines() }, { key: 'trip', vlu: val('ai-trip') }, { key: 'note', vlu: val('ai-note') }, { key: 'issue', vlu: issue ? '1' : '0' }]));
        };
        if (issue) {
            ask({ title: 'Issue this invoice?', text: 'It gets the next invoice number and can no longer be edited.', ok: 'Issue invoice', no: 'Not yet' }, go);
        } else {
            go();
        }
    }

    function saved(msg) {
        clean();
        toast(msg);
    }

    function pay(id) {
        var m = el('ai-f-method');
        var name = m.options[m.selectedIndex] ? m.options[m.selectedIndex].text : '';
        ask({ title: 'Mark this invoice paid?', text: m.value ? 'Paid by ' + name.toLowerCase() + '.' : '', ok: 'Mark paid', no: 'Cancel' }, function () {
            api('Pay', filters(collect('ai-pay', [{ key: 'id', vlu: id }])));
        });
        return false;
    }

    function voidIt(id) {
        ask({ title: 'Void this invoice?', text: 'The number stays used. Create a new draft from the job if you need a corrected invoice.', value: '', place: 'Reason', ok: 'Void invoice', no: 'Keep it', danger: true, check: function (v) { return v.length < 3 ? 'Give a reason.' : ''; } }, function (why) {
            api('VoidIt', filters([{ key: 'id', vlu: id }, { key: 'why', vlu: why }]));
        });
    }

    function discard(id) {
        ask({ title: 'Discard this draft?', text: 'The job goes back to the To invoice list.', ok: 'Discard draft', no: 'Keep it', danger: true }, function () {
            api('Discard', filters([{ key: 'id', vlu: id }]));
        });
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                if (askBox && askBox.classList.contains('ai-open')) {
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('ai-modal');
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
            if (e.target.closest && (e.target.closest('#ai-lines') || e.target.id === 'ai-trip')) {
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
        make: make,
        addLine: addLine,
        dropLine: dropLine,
        save: save,
        saved: saved,
        pay: pay,
        voidIt: voidIt,
        discard: discard
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminInvoicesJs.reveal);
} else {
    AdminInvoicesJs.reveal();
}

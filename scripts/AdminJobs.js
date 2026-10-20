var AdminJobsJs = (function () {

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
        $ApiRequest('AdminJobs/' + method, JSON.stringify(list));
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
        var t = el('aj-toast');
        t.textContent = msg;
        t.classList.add('aj-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('aj-open');
        }, 3600);
    }

    function openMenu() {
        el('aj-rail').classList.add('aj-open');
        el('aj-scrim').classList.add('aj-open');
    }

    function closeMenu() {
        el('aj-rail').classList.remove('aj-open');
        el('aj-scrim').classList.remove('aj-open');
    }

    function openModal(title) {
        var m = el('aj-modal');
        el('aj-mtitle').textContent = title || '';
        dirty = false;
        m.classList.add('aj-open');
        m.setAttribute('aria-hidden', 'false');
        m.scrollTop = 0;
    }

    function shut() {
        var m = el('aj-modal');
        if (m) {
            m.classList.remove('aj-open');
            m.setAttribute('aria-hidden', 'true');
        }
        dirty = false;
    }

    function closeModal() {
        var m = el('aj-modal');
        if (!m || !m.classList.contains('aj-open')) {
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
        if (!askBox || !askBox.classList.contains('aj-open')) {
            return;
        }
        var x = askParts(), f = askDone, v = x.i.value.trim();
        askBox.classList.remove('aj-open');
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
        askBox.className = 'aj-ask';
        askBox.innerHTML = '<div class="aj-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" aria-label="Answer"><em class="aj-err" role="alert"></em><div class="aj-askacts"><button type="button" class="aj-btn aj-btn-ghost"></button><button type="button" class="aj-btn"></button></div></div>';
        el('aj-root').appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () { askClose(false); });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () { x.e.textContent = ''; });
        askBox.addEventListener('mousedown', function (e) { if (e.target === askBox) { askClose(false); } });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('aj-open')) {
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
        x.b[1].className = 'aj-btn' + (o.danger ? ' aj-askdel' : '');
        askBox.firstChild.classList.toggle('aj-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        askBox.classList.add('aj-open');
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

    var st = 'upcoming', pg = 1, stimer = null;

    function filters(extra) {
        var list = [{ key: 'st', vlu: st }, { key: 'tech', vlu: val('aj-tech') }, { key: 'term', vlu: val('aj-term') }, { key: 'pg', vlu: String(pg) }];
        return extra ? list.concat(extra) : list;
    }

    function loaded(v) {
        st = v;
    }

    function view(v) {
        st = v;
        pg = 1;
        history.replaceState(null, '', 'AdminJobs?f=' + v);
        api('List', filters());
    }

    function filter() {
        pg = 1;
        api('List', filters());
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(filter, 250);
    }

    function page(n) {
        pg = n;
        api('List', filters());
    }

    function edit(id) {
        api('Edit', [{ key: 'id', vlu: id }]);
    }

    function opened(title) {
        var m = el('aj-modal');
        if (!m.classList.contains('aj-open')) {
            openModal(title);
        } else {
            el('aj-mtitle').textContent = title;
        }
    }

    function plan(qid) {
        api('PlanForm', [{ key: 'quote', vlu: qid }]);
    }

    function book(qid) {
        api('Book', filters(collect('aj-pform', [{ key: 'quote', vlu: qid }])));
        return false;
    }

    function saved(msg) {
        clean();
        toast(msg);
    }

    function move(id, dir, name) {
        var go = function () {
            api('Move', filters([{ key: 'id', vlu: id }, { key: 'dir', vlu: String(dir) }]));
        };
        if (dir < 0) {
            ask({ title: 'Step back to ' + name.toLowerCase() + '?', text: 'Use this when a status was set by mistake.', ok: 'Step back', no: 'Cancel' }, go);
        } else {
            ask({ title: 'Mark ' + name.toLowerCase() + '?', text: name === 'Done' ? 'The job is finished and can be invoiced.' : 'The office sees the new status right away.', ok: 'Mark ' + name.toLowerCase(), no: 'Not yet' }, go);
        }
    }

    function saveNotes(id) {
        api('SaveNotes', [{ key: 'id', vlu: id }, { key: 'notes', vlu: val('aj-f-notes') }]);
    }

    function replan() {
        var f = el('aj-replan');
        f.hidden = !f.hidden;
        if (!f.hidden) {
            f.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }
    }

    function replanSave(id) {
        api('Replan', filters(collect('aj-replan', [{ key: 'id', vlu: id }])));
        return false;
    }

    function cancelJob(id) {
        ask({ title: 'Cancel this job?', text: 'The quote goes back to the To schedule list so it can be booked again.', value: '', place: 'Reason (optional)', ok: 'Cancel job', no: 'Keep it', danger: true }, function (why) {
            api('CancelJob', filters([{ key: 'id', vlu: id }, { key: 'why', vlu: why }]));
        });
    }

    function reveal() {
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                if (askBox && askBox.classList.contains('aj-open')) {
                    return;
                }
                closeMenu();
                closeModal();
            }
        });
        var m = el('aj-modal');
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
        loaded: loaded,
        view: view,
        filter: filter,
        search: search,
        page: page,
        edit: edit,
        opened: opened,
        plan: plan,
        book: book,
        saved: saved,
        move: move,
        saveNotes: saveNotes,
        replan: replan,
        replanSave: replanSave,
        cancelJob: cancelJob
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminJobsJs.reveal);
} else {
    AdminJobsJs.reveal();
}

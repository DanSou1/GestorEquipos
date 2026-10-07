(function () {
    var STORAGE_KEY = 'ge_sidebar_collapsed';
    var MOBILE_QUERY = '(max-width: 768px)';

    var root = document.documentElement;
    var toggleBtn = document.getElementById('sidebarToggle');
    var backdrop = document.getElementById('sidebarBackdrop');

    if (!toggleBtn) {
        return;
    }

    function isMobile() {
        return window.matchMedia(MOBILE_QUERY).matches;
    }

    function setDesktopCollapsed(collapsed) {
        root.classList.toggle('sidebar-collapsed', collapsed);
        localStorage.setItem(STORAGE_KEY, collapsed ? '1' : '0');
        toggleBtn.setAttribute('aria-expanded', String(!collapsed));
    }

    function setMobileOpen(open) {
        root.classList.toggle('sidebar-mobile-open', open);
        toggleBtn.setAttribute('aria-expanded', String(open));
    }

    toggleBtn.setAttribute('aria-expanded', String(!root.classList.contains('sidebar-collapsed')));

    toggleBtn.addEventListener('click', function () {
        if (isMobile()) {
            setMobileOpen(!root.classList.contains('sidebar-mobile-open'));
        } else {
            setDesktopCollapsed(!root.classList.contains('sidebar-collapsed'));
        }
    });

    if (backdrop) {
        backdrop.addEventListener('click', function () {
            setMobileOpen(false);
        });
    }

    document.querySelectorAll('.app-sidebar__nav-chevron').forEach(function (chevron) {
        chevron.addEventListener('click', function (event) {
            event.preventDefault();
            var group = chevron.closest('.app-sidebar__nav-group');
            if (!group) {
                return;
            }
            var expanded = group.classList.toggle('is-expanded');
            chevron.setAttribute('aria-expanded', String(expanded));
        });
    });
})();

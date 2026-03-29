/**
 * Corvus.Text.Json Documentation Site — Main JavaScript
 */

document.addEventListener('DOMContentLoaded', () => {
  // Mobile navigation toggle
  const menuToggle = document.querySelector('.js-toggle-menu');
  const mobileNav = document.querySelector('.js-mobile-nav');

  if (menuToggle && mobileNav) {
    menuToggle.addEventListener('click', () => {
      const isOpen = mobileNav.classList.toggle('is-open');
      menuToggle.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
    });
  }

  // Mobile sidebar drawer toggle
  const sidebarToggle = document.querySelector('.sidebar-toggle');
  const sidebar = document.querySelector('.sidebar');
  const backdrop = document.querySelector('.sidebar-backdrop');

  function closeSidebar() {
    sidebar?.classList.remove('is-open');
    backdrop?.classList.remove('is-visible');
    sidebarToggle?.setAttribute('aria-expanded', 'false');
  }

  if (sidebarToggle && sidebar) {
    sidebarToggle.addEventListener('click', () => {
      const opening = !sidebar.classList.contains('is-open');
      sidebar.classList.toggle('is-open');
      backdrop?.classList.toggle('is-visible');
      sidebarToggle.setAttribute('aria-expanded', opening ? 'true' : 'false');
    });

    backdrop?.addEventListener('click', closeSidebar);

    // Close drawer when any link inside the sidebar is clicked (mobile)
    sidebar.addEventListener('click', (e) => {
      if (e.target.closest('a') && window.innerWidth < 960) {
        closeSidebar();
      }
    });
  }

  // Sidebar collapsible sections (namespace headings)
  document.querySelectorAll('.sidebar__heading').forEach((toggle) => {
    toggle.addEventListener('click', () => {
      const section = toggle.closest('.sidebar__section');
      const body = section?.querySelector('.sidebar__body');
      if (body) {
        body.classList.toggle('is-collapsed');
        toggle.classList.toggle('is-collapsed');
      }
    });
  });

  // Sidebar collapsible member categories
  document.querySelectorAll('.sidebar__cat-toggle').forEach((toggle) => {
    toggle.addEventListener('click', () => {
      const body = toggle.nextElementSibling;
      if (body && body.classList.contains('sidebar__cat-body')) {
        body.classList.toggle('is-collapsed');
        toggle.classList.toggle('is-collapsed');
      }
    });
  });

  // ── Footer-aware sidebar height ─────────────────────────────────────────
  // When the footer scrolls into view, shrink the sidebar so it doesn't
  // overlap. Measures actual distance from sidebar top to footer top.
  const sidebarEl = document.querySelector('.sidebar');
  const footer = document.querySelector('.site-footer');

  if (sidebarEl && footer && window.matchMedia('(min-width: 60rem)').matches) {
    const gap = 16; // breathing room (px) between sidebar bottom and footer

    function adjustSidebarHeight() {
      const sidebarTop = sidebarEl.getBoundingClientRect().top;
      const footerTop = footer.getBoundingClientRect().top;
      const fullHeight = window.innerHeight - sidebarTop - gap;
      const availableToFooter = footerTop - sidebarTop - gap;

      if (availableToFooter < fullHeight) {
        // Footer is encroaching — shrink sidebar
        const h = Math.max(100, availableToFooter);
        sidebarEl.style.setProperty('--sidebar-available-height', h + 'px');
      } else {
        // Footer is far away — use full viewport height
        sidebarEl.style.removeProperty('--sidebar-available-height');
      }
    }

    window.addEventListener('scroll', () => {
      requestAnimationFrame(adjustSidebarHeight);
    }, { passive: true });
    window.addEventListener('resize', adjustSidebarHeight, { passive: true });
    adjustSidebarHeight();
  }

  // Smooth scroll for anchor links
  document.querySelectorAll('a[href^="#"]').forEach((anchor) => {
    anchor.addEventListener('click', (e) => {
      const target = document.querySelector(anchor.getAttribute('href'));
      if (target) {
        e.preventDefault();
        target.scrollIntoView({ behavior: 'smooth', block: 'start' });
        history.pushState(null, '', anchor.getAttribute('href'));
      }
    });
  });

  // Copy code button — wraps <pre> in a container, button sits outside
  document.querySelectorAll('pre code').forEach((block) => {
    const pre = block.parentElement;
    const wrapper = document.createElement('div');
    wrapper.className = 'code-block';
    pre.parentNode.insertBefore(wrapper, pre);
    wrapper.appendChild(pre);
    const button = document.createElement('button');
    button.className = 'code-block__copy';
    button.setAttribute('aria-label', 'Copy code');
    button.innerHTML = '<svg width="14" height="14" viewBox="0 0 16 16" fill="currentColor"><path d="M0 6.75C0 5.784.784 5 1.75 5h1.5a.75.75 0 0 1 0 1.5h-1.5a.25.25 0 0 0-.25.25v7.5c0 .138.112.25.25.25h7.5a.25.25 0 0 0 .25-.25v-1.5a.75.75 0 0 1 1.5 0v1.5A1.75 1.75 0 0 1 9.25 16h-7.5A1.75 1.75 0 0 1 0 14.25Z"/><path d="M5 1.75C5 .784 5.784 0 6.75 0h7.5C15.216 0 16 .784 16 1.75v7.5A1.75 1.75 0 0 1 14.25 11h-7.5A1.75 1.75 0 0 1 5 9.25Zm1.75-.25a.25.25 0 0 0-.25.25v7.5c0 .138.112.25.25.25h7.5a.25.25 0 0 0 .25-.25v-7.5a.25.25 0 0 0-.25-.25Z"/></svg>';
    button.addEventListener('click', () => {
      navigator.clipboard.writeText(block.textContent).then(() => {
        button.innerHTML = '<svg width="14" height="14" viewBox="0 0 16 16" fill="currentColor"><path d="M13.78 4.22a.75.75 0 0 1 0 1.06l-7.25 7.25a.75.75 0 0 1-1.06 0L2.22 9.28a.75.75 0 0 1 1.06-1.06L6 10.94l6.72-6.72a.75.75 0 0 1 1.06 0Z"/></svg>';
        setTimeout(() => { button.innerHTML = '<svg width="14" height="14" viewBox="0 0 16 16" fill="currentColor"><path d="M0 6.75C0 5.784.784 5 1.75 5h1.5a.75.75 0 0 1 0 1.5h-1.5a.25.25 0 0 0-.25.25v7.5c0 .138.112.25.25.25h7.5a.25.25 0 0 0 .25-.25v-1.5a.75.75 0 0 1 1.5 0v1.5A1.75 1.75 0 0 1 9.25 16h-7.5A1.75 1.75 0 0 1 0 14.25Z"/><path d="M5 1.75C5 .784 5.784 0 6.75 0h7.5C15.216 0 16 .784 16 1.75v7.5A1.75 1.75 0 0 1 14.25 11h-7.5A1.75 1.75 0 0 1 5 9.25Zm1.75-.25a.25.25 0 0 0-.25.25v7.5c0 .138.112.25.25.25h7.5a.25.25 0 0 0 .25-.25v-7.5a.25.25 0 0 0-.25-.25Z"/></svg>'; }, 2000);
      });
    });
    wrapper.appendChild(button);
  });

  // Mark active sidebar link and expand its parent section
  const currentPath = window.location.pathname;
  document.querySelectorAll('.sidebar__link').forEach((link) => {
    if (link.getAttribute('href') === currentPath) {
      link.classList.add('is-active');

      // Expand the parent namespace section
      const section = link.closest('.sidebar__section');
      if (section) {
        const heading = section.querySelector('.sidebar__heading');
        const body = section.querySelector('.sidebar__body');
        if (heading) heading.classList.remove('is-collapsed');
        if (body) body.classList.remove('is-collapsed');
      }

      // If this is a type link, expand its member sub-tree
      const typeItem = link.closest('.sidebar__item');
      const memberTree = typeItem?.querySelector('.sidebar__members');
      if (memberTree) {
        memberTree.removeAttribute('hidden');
        memberTree.querySelectorAll('.sidebar__cat-toggle.is-collapsed').forEach((toggle) => {
          toggle.classList.remove('is-collapsed');
          const catBody = toggle.nextElementSibling;
          if (catBody) catBody.classList.remove('is-collapsed');
        });
      }

      // If this is a member link, also expand the parent type's member tree
      if (link.classList.contains('sidebar__link--member')) {
        const parentTypeItem = link.closest('.sidebar__item')?.parentElement?.closest('.sidebar__item');
        if (parentTypeItem) {
          const parentTypeLink = parentTypeItem.querySelector(':scope > .sidebar__link--type');
          if (parentTypeLink) parentTypeLink.classList.add('is-current');
          const parentMemberTree = parentTypeItem.querySelector(':scope > .sidebar__members');
          if (parentMemberTree) {
            parentMemberTree.removeAttribute('hidden');
            // Expand the category containing the active member
            const activeCat = link.closest('.sidebar__cat-section');
            if (activeCat) {
              const catToggle = activeCat.querySelector('.sidebar__cat-toggle');
              const catBody = activeCat.querySelector('.sidebar__cat-body');
              if (catToggle) catToggle.classList.remove('is-collapsed');
              if (catBody) catBody.classList.remove('is-collapsed');
            }
          }
        }
      }

      // Scroll into view
      requestAnimationFrame(() => {
        link.scrollIntoView({ block: 'center', behavior: 'instant' });
      });
    }
  });

  // ── On-page TOC in sidebar ──────────────────────────────────────────────
  // For doc pages: extract h2 headings, insert as a sub-list under the
  // active sidebar link, and highlight the current section on scroll.
  // Skip when the sidebar has server-side member navigation (API pages).
  const hasMemberNav = document.querySelector('[data-has-member-nav]');
  const activeLink = document.querySelector('.sidebar__link.is-active');
  const docContent = document.querySelector('.doc__content');

  if (activeLink && docContent && !hasMemberNav) {
    const headings = docContent.querySelectorAll('h2[id]');
    if (headings.length > 1) {
      const subList = document.createElement('ul');
      subList.className = 'sidebar__sublist';

      headings.forEach((h) => {
        const li = document.createElement('li');
        li.className = 'sidebar__item sidebar__item--toc';
        const a = document.createElement('a');
        a.className = 'sidebar__link sidebar__link--toc';
        a.href = '#' + h.id;
        a.textContent = h.textContent;
        li.appendChild(a);
        subList.appendChild(li);
      });

      // Insert sub-list after the active link's parent <li>
      activeLink.closest('.sidebar__item').appendChild(subList);

      // Scroll-spy: highlight the heading nearest the top of the viewport.
      // Uses a scroll listener (throttled via rAF) instead of
      // IntersectionObserver so clicks that land the heading at the very
      // top are always detected.
      const tocLinks = subList.querySelectorAll('.sidebar__link--toc');
      const headingArr = Array.from(headings);
      let ticking = false;

      function updateActiveToc() {
        const scrollY = window.scrollY;
        const offset = 100; // px below top to count as "current"
        let current = headingArr[0];

        for (const h of headingArr) {
          if (h.getBoundingClientRect().top <= offset) {
            current = h;
          } else {
            break;
          }
        }

        tocLinks.forEach((l) => l.classList.remove('is-current'));
        if (current) {
          const match = subList.querySelector(`a[href="#${current.id}"]`);
          if (match) {
            match.classList.add('is-current');
            // Scroll the sidebar so the active TOC item stays visible
            match.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
          }
        }
        ticking = false;
      }

      window.addEventListener('scroll', () => {
        if (!ticking) {
          ticking = true;
          requestAnimationFrame(updateActiveToc);
        }
      }, { passive: true });

      // Initial highlight
      updateActiveToc();
    }
  }
});

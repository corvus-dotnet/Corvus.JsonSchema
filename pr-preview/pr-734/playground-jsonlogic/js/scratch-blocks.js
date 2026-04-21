// scratch-blocks.js — Draws SVG outlines for Scratch-style C-blocks.
// Uses Scratch's actual path constants (corner radius, notch shapes).
(function () {
    'use strict';

    const R = 4;          // corner radius (from Scratch source)
    const SW = 16;        // spine width (left bar of C)
    const NOTCH = 'l 6,4 3,0 6,-4';           // top/bottom notch (LTR)
    const NOTCH_REV = 'l -6,4 -3,0 -6,-4';    // inner mouth notch (RTL)
    const NOTCH_W = 15;   // total width of notch

    /**
     * Generates the SVG path for a Scratch-style C-block.
     * The path traces clockwise: top edge → right side → (header→mouth pairs) → bottom edge → left side → close.
     * Mouth cutouts are created by the path going AROUND them.
     *
     * @param {number} W  Total width in px.
     * @param {Array<{type:'header'|'mouth'|'footer', h:number}>} sections
     *    Ordered list of sections with measured heights.
     * @returns {string}  SVG path data.
     */
    function cblockPath(W, sections) {
        const p = [];
        let y = 0;

        // ── Top edge: top-left corner, notch, top-right corner ──
        p.push(`M 0,${R}`);
        p.push(`a ${R},${R} 0 0,1 ${R},-${R}`);   // top-left corner
        p.push(NOTCH);                               // top notch
        p.push(`H ${W - R}`);                        // top edge to right
        p.push(`a ${R},${R} 0 0,1 ${R},${R}`);     // top-right corner

        for (let i = 0; i < sections.length; i++) {
            const s = sections[i];
            const next = sections[i + 1];

            if (s.type === 'header' && next && next.type === 'mouth') {
                // ── Header followed by mouth: go down right side, cut into mouth ──
                y += s.h;
                p.push(`V ${y - R}`);
                // Inner top-right corner of mouth
                p.push(`a ${R},${R} 0 0,1 -${R},${R}`);
                // Inner top edge: right to left with reverse notch
                p.push(`H ${SW + NOTCH_W + R}`);
                p.push(NOTCH_REV);
                p.push(`H ${SW + R}`);
                // Inner top-left corner of mouth
                p.push(`a ${R},${R} 0 0,0 -${R},${R}`);

            } else if (s.type === 'mouth') {
                // ── Mouth: spine descends on left, then cut back to right ──
                y += s.h;
                p.push(`V ${y - R}`);
                // Inner bottom-left corner of mouth
                p.push(`a ${R},${R} 0 0,0 ${R},${R}`);
                // Inner bottom edge: left to right
                p.push(`H ${W - R}`);
                // Inner bottom-right corner (rejoin right edge)
                p.push(`a ${R},${R} 0 0,1 ${R},${R}`);

            } else {
                // ── Footer or other header: simple descent on right edge ──
                y += s.h;
                // Don't emit V here — the bottom-edge code or the next section handles positioning
            }
        }

        // ── Bottom edge ──
        p.push(`V ${y - R}`);
        p.push(`a ${R},${R} 0 0,1 -${R},${R}`);    // bottom-right corner
        p.push(`H ${NOTCH_W + R}`);                   // bottom edge to left
        p.push(NOTCH_REV);                             // bottom notch
        p.push(`H ${R}`);
        p.push(`a ${R},${R} 0 0,1 -${R},-${R}`);   // bottom-left corner
        p.push('Z');

        return p.join(' ');
    }

    /**
     * Measures a .block-cblock element and (re)draws its SVG background.
     */
    function renderCBlock(el) {
        const W = el.offsetWidth;
        if (W === 0) return;   // not visible yet

        // Measure sections from child elements
        const sections = [];
        for (const child of el.children) {
            if (child.tagName === 'svg') continue;
            const h = child.offsetHeight;
            if (child.classList.contains('cblock-mouth')) {
                sections.push({ type: 'mouth', h });
            } else if (child.classList.contains('cblock-footer')) {
                sections.push({ type: 'footer', h });
            } else {
                sections.push({ type: 'header', h });
            }
        }

        if (sections.length === 0) return;

        const totalH = sections.reduce((sum, s) => sum + s.h, 0);
        const d = cblockPath(W, sections);

        // Get or create SVG element
        let svg = el.querySelector(':scope > svg.cblock-svg');
        if (!svg) {
            svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
            svg.classList.add('cblock-svg');
            const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            svg.appendChild(path);
            el.prepend(svg);
        }

        svg.setAttribute('width', W);
        svg.setAttribute('height', totalH);
        svg.setAttribute('viewBox', `0 0 ${W} ${totalH}`);

        const path = svg.querySelector('path');
        path.setAttribute('d', d);

        // Read colours from CSS custom properties
        const style = getComputedStyle(el);
        const fill = style.getPropertyValue('--cb-fill').trim() || '#FFAB19';
        const stroke = style.getPropertyValue('--cb-stroke').trim() || '#CF8B17';
        path.setAttribute('fill', fill);
        path.setAttribute('stroke', stroke);
        path.setAttribute('stroke-width', '1.5');
    }

    // ── Auto-discovery and live updates ──

    const observer = new ResizeObserver(entries => {
        for (const entry of entries) {
            const el = entry.target;
            if (el.classList.contains('block-cblock')) {
                renderCBlock(el);
            } else {
                // Child resized — re-render parent C-block
                const parent = el.closest('.block-cblock');
                if (parent) renderCBlock(parent);
            }
        }
    });

    const mutObs = new MutationObserver(mutations => {
        for (const m of mutations) {
            for (const node of m.addedNodes) {
                if (node.nodeType !== 1) continue;
                if (node.classList && node.classList.contains('block-cblock')) {
                    observeBlock(node);
                }
                if (node.querySelectorAll) {
                    node.querySelectorAll('.block-cblock').forEach(observeBlock);
                }
            }
        }
    });

    function observeBlock(el) {
        renderCBlock(el);
        observer.observe(el);
        // Also observe children so mouth/header height changes re-trigger
        for (const child of el.children) {
            if (child.tagName !== 'svg') {
                observer.observe(child);
            }
        }
    }

    // Initial scan
    function init() {
        document.querySelectorAll('.block-cblock').forEach(observeBlock);
        mutObs.observe(document.body, { childList: true, subtree: true });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

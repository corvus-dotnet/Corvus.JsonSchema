# ADR 0041. Standards-only, zero-build custom elements

Date: 2026-07-21. Status: **Accepted**. Scope: the technology the web kit is built on. Builds on
[ADR 0040](0040-three-layer-web-kit.md). This records why the kit is standards-only custom elements with no
build step and no framework dependency.

## Context

A UI kit is only reusable if it drops into whatever an application already uses. Binding it to a framework
(React, Angular, Vue) or a build toolchain (a bundler, a transpiler, a component compiler) narrows its audience
to applications that already use that stack, and dates the kit as that stack moves. The kit's job is to work in
any application with no adoption tax.

### Grounded architectural facts

- **Standards-only custom elements.** The kit is a zero-build kit of composable web components
  (design `ui-design.md`). Each element extends `HTMLElement` and registers through `define(tagName, ctor)`
  (`web/arazzo-control-plane-ui/src/components/base.js`), so it is a native custom element with no framework
  runtime.
- **Open shadow DOM with CSS-token theming.** `ArazzoElement` uses an open shadow root, and styling is through
  CSS custom properties (`--arazzo-*` tokens) that a themed ancestor supplies, so a component inherits the
  host's theme without a styling framework.
- **No build step.** The kit ships as ES modules a browser loads directly. Heavier vendored dependencies (the
  code editor, the layout engine) are lazy-loaded ESM, not a bundler requirement.

## Decision

The web kit is **standards-only custom elements with no build step and no framework dependency**. Each piece is
a native custom element extending `HTMLElement`, styled through CSS custom properties in an open shadow root,
shipped as ES modules a browser loads directly. It works in any application, framework or not, with no bundler.

## Consequences

- The kit drops into any application, whatever framework or none it uses, because a custom element is a browser
  primitive.
- There is no adoption tax: no build configuration, no framework version to match, no compiler.
- Theming is by CSS tokens, so a host themes the whole kit by setting `--arazzo-*` properties, and light and
  dark are token sets, not component variants.
- The zero-dependency ethos carries into the designer: where a rich surface is needed it is built first-party
  rather than pulled from a library ([ADR 0043](0043-first-party-svg-design-surface.md)).

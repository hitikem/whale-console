/* ═══════════════════════════════════════════════════════════════════
   液态玻璃（Liquid Glass）—— 移植自 nikdelvin/liquid-glass
   原理：用 SVG feDisplacementMap 生成折射位移图，作为 backdrop-filter
   ═══════════════════════════════════════════════════════════════════ */

/**
 * 生成位移贴图（displacement map）
 * 红通道 = X 方向位移，绿通道 = Y 方向位移，0x80 为中性（不偏移）
 */
function getDisplacementMap({ height, width, radius, depth }) {
  // 不用 mix-blend-mode（在 data URI SVG 中不可靠）
  // 直接用两层渐变 + 中央中性区构造位移图
  return 'data:image/svg+xml;utf8,' + encodeURIComponent(`<svg height="${height}" width="${width}" viewBox="0 0 ${width} ${height}" xmlns="http://www.w3.org/2000/svg">
    <defs>
      <!-- X 方向：左红(255,0,0) → 中间中性(128) → 右黑(0,0,0) -->
      <linearGradient id="X" x1="0" y1="0" x2="1" y2="0">
        <stop offset="0%"    stop-color="rgb(255,128,128)" />
        <stop offset="${(radius / width) * 100}%"  stop-color="rgb(128,128,128)" />
        <stop offset="${100 - (radius / width) * 100}%" stop-color="rgb(128,128,128)" />
        <stop offset="100%"  stop-color="rgb(0,128,128)" />
      </linearGradient>
      <!-- Y 方向：上绿 → 中间中性 → 下黑 -->
      <linearGradient id="Y" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0%"    stop-color="rgb(128,255,128)" />
        <stop offset="${(radius / height) * 100}%"  stop-color="rgb(128,128,128)" />
        <stop offset="${100 - (radius / height) * 100}%" stop-color="rgb(128,128,128)" />
        <stop offset="100%"  stop-color="rgb(128,0,128)" />
      </linearGradient>
    </defs>
    <!-- 基础：X 渐变 -->
    <rect x="0" y="0" height="${height}" width="${width}" fill="url(#X)" />
    <!-- 叠加：Y 渐变（用 opacity 柔化，避免覆盖） -->
    <rect x="0" y="0" height="${height}" width="${width}" fill="url(#Y)" opacity="0.5" />
    <!-- 圆角遮罩：外侧让位移归零 -->
    <rect
        x="${depth}" y="${depth}"
        height="${height - 2 * depth}" width="${width - 2 * depth}"
        fill="#808080" rx="${radius}" ry="${radius}"
        filter="blur(${depth}px)"
    />
</svg>`);
}
function getDisplacementFilter({ height, width, radius, depth, strength = 100, chromaticAberration = 0 }) {
  return 'data:image/svg+xml;utf8,' + encodeURIComponent(`<svg height="${height}" width="${width}" viewBox="0 0 ${width} ${height}" xmlns="http://www.w3.org/2000/svg">
    <defs>
        <filter id="displace" color-interpolation-filters="sRGB">
            <feImage x="0" y="0" height="${height}" width="${width}" href="${getDisplacementMap({ height, width, radius, depth })}" result="displacementMap" />
            <feDisplacementMap
                transform-origin="center"
                in="SourceGraphic"
                in2="displacementMap"
                scale="${strength + chromaticAberration * 2}"
                xChannelSelector="R"
                yChannelSelector="G"
            />
            <feColorMatrix
            type="matrix"
            values="1 0 0 0 0
                    0 0 0 0 0
                    0 0 0 0 0
                    0 0 0 1 0"
            result="displacedR"
                    />
            <feDisplacementMap
                in="SourceGraphic"
                in2="displacementMap"
                scale="${strength + chromaticAberration}"
                xChannelSelector="R"
                yChannelSelector="G"
            />
            <feColorMatrix
            type="matrix"
            values="0 0 0 0 0
                    0 1 0 0 0
                    0 0 0 0 0
                    0 0 0 1 0"
            result="displacedG"
                    />
            <feDisplacementMap
                    in="SourceGraphic"
                    in2="displacementMap"
                    scale="${strength}"
                    xChannelSelector="R"
                    yChannelSelector="G"
                />
                <feColorMatrix
                type="matrix"
                values="0 0 0 0 0
                        0 0 0 0 0
                        0 0 1 0 0
                        0 0 0 1 0"
                result="displacedB"
                        />
              <feBlend in="displacedR" in2="displacedG" mode="screen"/>
              <feBlend in2="displacedB" mode="screen"/>
        </filter>
    </defs>
</svg>`) + '#displace';
}

/* ── 检测浏览器是否支持 backdrop-filter: url(#id) ── */
const supportsBackdropFilterUrl = (() => {
  const el = document.createElement('div');
  el.style.cssText = 'backdrop-filter: url(#test)';
  return el.style.backdropFilter === 'url(#test)' || el.style.backdropFilter === 'url("#test")';
})();

/**
 * 重绘单个玻璃元素
 */
function redrawGlass(el) {
  // 用元素自身的尺寸（不是 .lg-content，后者可能被隐藏或尚未布局）
  const rect = el.getBoundingClientRect();
  const width = Math.max(1, Math.round(rect.width));
  const height = Math.max(1, Math.round(rect.height));

  // 尺寸为 0 说明还没布局好，跳过
  if (width < 4 || height < 4) return;

  const layer = el.querySelector('.glass-box');
  if (!layer) return;

  const blur = parseFloat(layer.dataset.blur || '2');
  const cab = parseFloat(layer.dataset.cab || '4');
  const depth = parseFloat(layer.dataset.depth || '8');
  const strength = parseFloat(layer.dataset.strength || '90');
  const saturate = parseFloat(layer.dataset.saturate || '1.6');
  const brightness = parseFloat(layer.dataset.brightness || '1.08');

  // 从元素自身取圆角
  const cs = getComputedStyle(el);
  const radius = parseFloat(cs.borderTopLeftRadius) || 16;

  layer.style.width = width + 'px';
  layer.style.height = height + 'px';

  if (supportsBackdropFilterUrl) {
    const filter = getDisplacementFilter({ height, width, radius, depth, strength, chromaticAberration: cab });
    layer.style.backdropFilter =
      `blur(${blur / 2}px) url('${filter}') blur(${blur}px) brightness(${brightness}) saturate(${saturate})`;
    layer.style.webkitBackdropFilter = layer.style.backdropFilter;
  } else {
    // 降级：普通毛玻璃
    const fallback = `blur(${Math.min(30, width / 12)}px) saturate(180%) brightness(${brightness})`;
    layer.style.backdropFilter = fallback;
    layer.style.webkitBackdropFilter = fallback;
  }
}

/**
 * 初始化页面上所有 .liquid-glass 元素
 */
function redrawAll() {
  document.querySelectorAll('.liquid-glass').forEach(redrawGlass);
}

function initLiquidGlass() {
  const all = document.querySelectorAll('.liquid-glass');
  all.forEach((el) => {
    redrawGlass(el);
    new ResizeObserver(() => {
      // 用 rAF 避免 ResizeObserver 循环
      requestAnimationFrame(() => redrawGlass(el));
    }).observe(el);
  });

  // 多次延时重绘，等字体/布局完全稳定
  [50, 150, 400, 900, 1800].forEach((t) => setTimeout(redrawAll, t));

  // 窗口尺寸变化也重绘
  window.addEventListener('resize', () => {
    document.querySelectorAll('.liquid-glass').forEach(redrawGlass);
  });

  // 暴露给外部，新增元素后手动调用
  window.__redrawLiquidGlass = redrawAll;
}

/* 启动 */
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initLiquidGlass);
} else {
  initLiquidGlass();
}

(function () {
  var hosts = document.querySelectorAll('[data-mk-map]');
  if (!hosts.length || !window.d3 || !window.topojson) return;

  var TYPES = ['SQL Injection', 'Brute Force SSH', 'Credential Stuffing', 'XSS Reflected', 'Port Scan', 'DDoS L7 Flood', 'Path Traversal', 'Log4Shell Probe'];
  var COLORS = ['#FF3B5C', '#FF8A3D', '#4D9BFF'];

  d3.json('/lib/d3/countries-110m.json').then(function (topo) {
    var geo = topojson.feature(topo, topo.objects.countries);
    hosts.forEach(function (host) { setup(host, geo); });
  }).catch(function () { /* mapa é decorativo — falha de rede não quebra a página */ });

  function setup(host, geo) {
    var data = {};
    try { data = JSON.parse(host.getAttribute('data-mk-map') || '{}'); } catch (e) { data = {}; }
    var origins = data.origins || [];
    var dcs = data.dcs || [];
    var live = host.hasAttribute('data-mk-map-live');

    var W = 960, H = host.clientHeight > 40 ? host.clientHeight : 300;
    host.innerHTML = '';
    var svg = d3.select(host).append('svg')
      .attr('viewBox', '0 0 ' + W + ' ' + H).attr('preserveAspectRatio', 'xMidYMid meet')
      .style('width', '100%').style('height', '100%').style('display', 'block');

    var proj = d3.geoNaturalEarth1().fitExtent([[16, 12], [W - 16, H - 12]], geo);
    svg.append('g').selectAll('path').data(geo.features).enter().append('path')
      .attr('d', d3.geoPath(proj)).attr('fill', '#0F1F33').attr('stroke', '#1B3352').attr('stroke-width', 0.5);

    var g = svg.append('g');
    dcs.forEach(function (d) {
      var p = proj([d.lng, d.lat]);
      if (!p) return;
      g.append('circle').attr('cx', p[0]).attr('cy', p[1]).attr('r', 3).attr('fill', '#00E0A4');
      g.append('circle').attr('cx', p[0]).attr('cy', p[1]).attr('r', 3).attr('fill', 'none')
        .attr('stroke', '#00E0A4').attr('stroke-width', 1).attr('opacity', .5)
        .attr('style', 'transform-box:fill-box; transform-origin:center; animation:mk-pulse 2.6s ease-out infinite');
      g.append('text').attr('x', p[0] + 6).attr('y', p[1] + 3).attr('fill', '#376B62')
        .attr('font-size', '8px').attr('font-family', 'IBM Plex Mono, monospace').text(d.code);
    });

    if (!live || !origins.length || !dcs.length) return;

    var arcs = svg.append('g');
    function r(n) { return Math.floor(Math.random() * n); }
    function fire() {
      var o = origins[r(origins.length)];
      var dc = dcs[r(dcs.length)];
      var a = proj([o.lng, o.lat]), b = proj([dc.lng, dc.lat]);
      if (!a || !b) return;
      var col = COLORS[r(COLORS.length)];
      var mx = (a[0] + b[0]) / 2, my = (a[1] + b[1]) / 2 - Math.abs(a[0] - b[0]) * .24 - 16;
      var path = arcs.append('path')
        .attr('d', 'M' + a[0] + ',' + a[1] + ' Q' + mx + ',' + my + ' ' + b[0] + ',' + b[1])
        .attr('fill', 'none').attr('stroke', col).attr('stroke-width', 1.1).attr('opacity', .85);
      var node = path.node(), len = node.getTotalLength();
      node.setAttribute('stroke-dasharray', len);
      node.setAttribute('stroke-dashoffset', len);
      node.animate([{ strokeDashoffset: len }, { strokeDashoffset: 0 }], { duration: 1000, easing: 'cubic-bezier(.3,.7,.4,1)', fill: 'forwards' });
      var ring = arcs.append('circle').attr('cx', a[0]).attr('cy', a[1]).attr('r', 2.4)
        .attr('fill', 'none').attr('stroke', col).attr('stroke-width', 1);
      ring.node().animate([{ r: 2.4, opacity: .9 }, { r: 13, opacity: 0 }], { duration: 1500, easing: 'ease-out' });
      var dot = arcs.append('circle').attr('cx', a[0]).attr('cy', a[1]).attr('r', 2.2).attr('fill', col);
      setTimeout(function () {
        node.animate([{ opacity: .85 }, { opacity: 0 }], { duration: 500, fill: 'forwards' });
        setTimeout(function () { path.remove(); ring.remove(); dot.remove(); }, 520);
      }, 1100);
    }
    var timer = setInterval(fire, 2200);
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (en) {
        if (!en.isIntersecting) { clearInterval(timer); } else { clearInterval(timer); timer = setInterval(fire, 2200); }
      });
    }, { threshold: 0 });
    io.observe(host);
  }
})();

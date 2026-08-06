(function () {
  var host = document.getElementById('oi-map-host');
  if (!host || !window.d3 || !window.topojson) return;

  var dados = {};
  try { dados = JSON.parse(host.getAttribute('data-mapa') || '{}'); } catch (e) { dados = {}; }
  var origens = dados.origens || [];
  var datacenters = dados.datacenters || [];

  d3.json('/lib/d3/countries-110m.json').then(function (topo) {
    var geo = topojson.feature(topo, topo.objects.countries);
    desenhar(geo);
  }).catch(function () { /* mapa é decorativo — falha de rede não quebra o dashboard */ });

  function desenhar(geo) {
    var W = 980, H = host.clientHeight > 40 ? host.clientHeight : 320;
    host.innerHTML = '';
    var svg = d3.select(host).append('svg')
      .attr('viewBox', '0 0 ' + W + ' ' + H)
      .attr('preserveAspectRatio', 'xMidYMid meet')
      .style('width', '100%').style('height', '100%').style('display', 'block');

    var proj = d3.geoNaturalEarth1().fitExtent([[14, 10], [W - 14, H - 10]], geo);
    var path = d3.geoPath(proj);

    svg.append('g').selectAll('path').data(geo.features).enter().append('path')
      .attr('d', path).attr('fill', '#111C26').attr('stroke', '#1B2C3A').attr('stroke-width', 0.5);

    var dcg = svg.append('g');
    datacenters.forEach(function (d) {
      var p = proj([d.lng, d.lat]);
      if (!p) return;
      dcg.append('circle').attr('cx', p[0]).attr('cy', p[1]).attr('r', 3.2)
        .attr('fill', '#00E0A4').attr('stroke', '#06251E').attr('stroke-width', 1);
      dcg.append('text').attr('x', p[0] + 6).attr('y', p[1] + 3)
        .attr('fill', '#3E6C60').attr('font-size', '8px')
        .attr('font-family', 'IBM Plex Mono, monospace').text(d.codigo);
    });

    var maxCount = origens.reduce(function (m, o) { return Math.max(m, o.count); }, 1);
    var og = svg.append('g');
    origens.forEach(function (o) {
      var p = proj([o.lng, o.lat]);
      if (!p) return;
      var r = 3 + (o.count / maxCount) * 7;
      og.append('circle').attr('cx', p[0]).attr('cy', p[1]).attr('r', r)
        .attr('fill', '#FF3B5C').attr('opacity', 0.75).attr('stroke', '#2A0D14').attr('stroke-width', 1);
      og.append('circle').attr('cx', p[0]).attr('cy', p[1]).attr('r', r + 4)
        .attr('fill', 'none').attr('stroke', '#FF3B5C').attr('stroke-width', 1).attr('opacity', 0.35);
    });
  }
})();

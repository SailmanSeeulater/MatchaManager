-- Claude's rendition of the Matcha Manager background (480x270, layered)
local W, H = 480, 270
local out = app.params["out"]
local spr = Sprite(W, H, ColorMode.RGB)

local function C(s, a)
  return app.pixelColor.rgba(tonumber(s:sub(1,2),16), tonumber(s:sub(3,4),16), tonumber(s:sub(5,6),16), a or 255)
end

-- palette
local P = {
  black="000000", ink="1e1a2b", frame="f0eef5",
  wall="4a3346", wallLt="563d52", wood="8a5432", woodDk="5e3720", woodLt="a8693f",
  counter="eef3fa", counterSh="c3cfdf",
  mDk="3f6b2e", mDk2="2e5222", m="6fa04a", mLt="a6cf7a", mPale="dcebc0",
  cream="f3ead6", creamDk="e0d2b2", white="ffffff",
  red="b8423f", redDk="8a2e2c", redLt="d9705a", cyan="8fd9e8", cyanDk="5ec8dd",
  gray="8a9096", grayDk="40454a", grayLt="b8bec3", screen="1f2a38",
  yellow="f2d14b", orange="f08a3c", skin="f0c39a", skinDk="d49c72", hair="4a2c1d", blush="f09a9a",
  shirt="5e74e0", shirtDk="4a5cc0", station="9fb1b8", stationLn="97a9b0", stationDk="7d8f96",
  glass="bdeffd", ice="e4f8fc", sky="bfe3f2", terracotta="c56a3c", terraDk="9a4f2a",
  paperLn="e3d6b8", disabled="c9d3d7",
  skin2="c68a5e", skin2Dk="a06a44", skin3="f6d6b8", skin3Dk="dcb090", hairBlack="2a2230", blonde="e0b050",
}
for k, v in pairs(P) do P[k] = C(v) end

-- drawing helpers ---------------------------------------------------------
local cur
local function px(x, y, c) if x >= 0 and y >= 0 and x < W and y < H then cur:drawPixel(x, y, c) end end
local function fill(x, y, w, h, c) for j = y, y+h-1 do for i = x, x+w-1 do px(i, j, c) end end end
local function box(x, y, w, h, c)
  for i = x, x+w-1 do px(i, y, c); px(i, y+h-1, c) end
  for j = y, y+h-1 do px(x, j, c); px(x+w-1, j, c) end
end
local function rfill(x, y, w, h, c, r)
  for j = 0, h-1 do for i = 0, w-1 do
    local dx = math.max(r-i, i-(w-1-r), 0); local dy = math.max(r-j, j-(h-1-r), 0)
    if dx*dx + dy*dy <= r*r then px(x+i, y+j, c) end
  end end
end
local function rbox(x, y, w, h, fillc, outc, r) rfill(x, y, w, h, outc, r); rfill(x+1, y+1, w-2, h-2, fillc, math.max(r-1, 0)) end
local function disc(cx, cy, r, c)
  for dy = -r, r do for dx = -r, r do if dx*dx + dy*dy <= r*r + r*0.6 then px(cx+dx, cy+dy, c) end end end
end
local function ellipse(cx, cy, rx, ry, c)
  for y = math.floor(cy-ry), math.ceil(cy+ry) do for x = math.floor(cx-rx), math.ceil(cx+rx) do
    local a, b = (x-cx)/rx, (y-cy)/ry
    if a*a + b*b <= 1.0 then px(x, y, c) end
  end end
end
local function line(x0, y0, x1, y1, c)
  local dx, dy = math.abs(x1-x0), -math.abs(y1-y0)
  local sx, sy = x0 < x1 and 1 or -1, y0 < y1 and 1 or -1
  local err = dx + dy
  while true do
    px(x0, y0, c)
    if x0 == x1 and y0 == y1 then break end
    local e2 = 2*err
    if e2 >= dy then err = err + dy; x0 = x0 + sx end
    if e2 <= dx then err = err + dx; y0 = y0 + sy end
  end
end

-- 3x5 pixel font ----------------------------------------------------------
local G = {
  A={".#.","#.#","###","#.#","#.#"}, B={"##.","#.#","##.","#.#","##."}, C={".##","#..","#..","#..",".##"},
  D={"##.","#.#","#.#","#.#","##."}, E={"###","#..","##.","#..","###"}, F={"###","#..","##.","#..","#.."},
  G={".##","#..","#.#","#.#",".##"}, H={"#.#","#.#","###","#.#","#.#"}, I={"###",".#.",".#.",".#.","###"},
  J={"..#","..#","..#","#.#",".#."}, K={"#.#","#.#","##.","#.#","#.#"}, L={"#..","#..","#..","#..","###"},
  M={"#...#","##.##","#.#.#","#...#","#...#"}, N={"#..#","##.#","#.##","#..#","#..#"},
  O={".#.","#.#","#.#","#.#",".#."}, P={"##.","#.#","##.","#..","#.."}, Q={".#.","#.#","#.#","##.",".##"},
  R={"##.","#.#","##.","#.#","#.#"}, S={".##","#..",".#.","..#","##."}, T={"###",".#.",".#.",".#.",".#."},
  U={"#.#","#.#","#.#","#.#","###"}, V={"#.#","#.#","#.#","#.#",".#."}, W={"#...#","#...#","#.#.#","##.##","#...#"},
  X={"#.#","#.#",".#.","#.#","#.#"}, Y={"#.#","#.#",".#.",".#.",".#."}, Z={"###","..#",".#.","#..","###"},
  ["0"]={"###","#.#","#.#","#.#","###"}, ["1"]={".#.","##.",".#.",".#.","###"}, ["2"]={"##.","..#",".#.","#..","###"},
  ["3"]={"##.","..#",".#.","..#","##."}, ["4"]={"#.#","#.#","###","..#","..#"}, ["5"]={"###","#..","##.","..#","##."},
  ["6"]={".##","#..","###","#.#","###"}, ["7"]={"###","..#",".#.",".#.",".#."}, ["8"]={"###","#.#","###","#.#","###"},
  ["9"]={"###","#.#","###","..#","##."}, [":"]={".","#",".","#","."}, ["."]={".",".",".",".","#"},
  [">"]={"#..",".#.","..#",".#.","#.."}, ["-"]={"...","...","###","...","..."}, ["+"]={"...",".#.","###",".#.","..."},
  ["/"]={"..#","..#",".#.","#..","#.."}, ["$"]={".##","##.",".#.",".##","##."}, [" "]={"..","..","..","..",".."},
  ["!"]={"#","#","#",".","#"}, ["?"]={"##.","..#",".#.","...",".#."}, ["'"]={"#","#",".",".","."},
  [","]={"..","..","..",".#","#."}, ["("]={".#","#.","#.","#.",".#"}, [")"]={"#.",".#",".#",".#","#."},
  ["["]={"##","#.","#.","#.","##"}, ["]"]={"##",".#",".#",".#","##"}, ["<"]={"..#",".#.","#..",".#.","..#"},
  ["%"]={"#.#","..#",".#.","#..","#.#"}, ["="]={"...","###","...","###","..."}, ['"']={"#.#","#.#","...","...","..."},
  [";"]={".","#",".","#","#"}, ["_"]={"...","...","...","...","###"}, ["*"]={"...","#.#",".#.","#.#","..."},
  ELLIPSIS={".....",".....",".....",".....","#.#.#"},
}
local function textWidth(s, sc)
  sc = sc or 1; local w = 0
  for i = 1, #s do w = w + #G[s:sub(i,i)][1] + 1 end
  return (w - 1) * sc
end
local function text(x, y, s, c, sc)
  sc = sc or 1
  for i = 1, #s do
    local g = G[s:sub(i,i)]
    for r = 1, 5 do for k = 1, #g[r] do
      if g[r]:sub(k,k) == "#" then fill(x + (k-1)*sc, y + (r-1)*sc, sc, sc, c) end
    end end
    x = x + (#g[1] + 1) * sc
  end
end
local function ctext(x, y, w, s, c, sc) text(x + math.floor((w - textWidth(s, sc)) / 2), y, s, c, sc) end

-- layer builder -----------------------------------------------------------
local firstLayer = true
local function layer(name, fn)
  local l
  if firstLayer then l = spr.layers[1]; firstLayer = false else l = spr:newLayer() end
  l.name = name
  cur = Image(W, H, ColorMode.RGB)
  fn()
  spr:newCel(l, 1, cur, Point(0, 0))
  return l
end

-- layout constants (kept from the original sprite) --------------------------
local SLOT_X, SLOT_W, SLOT_H = 304, 57, 24
local SLOT_Y = {4, 32, 60, 88, 116}
local BACK_X, BACK_Y = 418, 4
local SERVE_X, SERVE_Y, SERVE_R = 464, 147, 11

-- LAYERS ==================================================================
layer("Back_Background", function() fill(0, 0, W, H, P.black) end)

layer("Shop_Wall", function()
  fill(2, 2, 297, 135, P.wall)
  for x = 10, 298, 16 do for y = 2, 117 do px(x, y, P.wallLt) end end
  -- wainscot
  fill(2, 118, 297, 19, P.wood)
  fill(2, 118, 297, 2, P.woodLt)
  for x = 8, 298, 10 do for y = 121, 136 do px(x, y, P.woodDk) end end
  -- round window with bamboo
  disc(210, 56, 30, P.woodDk); disc(210, 56, 28, P.wood); disc(210, 56, 26, P.sky)
  for _, bx in ipairs({190, 196, 229}) do
    for y = 30, 82 do
      local dx, dy = bx - 210, y - 56
      if dx*dx + dy*dy <= 26*26 then px(bx, y, P.m); px(bx+1, y, P.mDk); if y % 11 == 0 then px(bx-1, y, P.mDk); px(bx+2, y, P.mDk) end end
    end
  end
  ellipse(193, 42, 4, 2, P.mLt); ellipse(201, 50, 4, 2, P.mLt); ellipse(226, 40, 4, 2, P.mLt)
  -- hanging lamp
  line(166, 2, 166, 15, P.ink)
  for j = 0, 7 do local hw = 3 + j; fill(166 - hw, 16 + j, hw*2 + 1, 1, j == 0 and P.ink or P.yellow) end
  fill(158, 23, 17, 1, P.ink)
  disc(166, 26, 2, P.cream)
end)

layer("Shelf", function()
  fill(8, 10, 142, 109, P.woodDk)
  box(8, 10, 142, 109, P.ink)
  fill(9, 11, 140, 2, P.woodLt)
  for _, by in ipairs({44, 78, 112}) do fill(9, by, 140, 4, P.wood); fill(9, by, 140, 1, P.woodLt) end
  fill(9, 11, 3, 107, P.wood); fill(146, 11, 3, 107, P.wood)
  local tins = {
    {body=P.m, band=P.cream, lid=P.mDk, w=12, h=22},
    {body=P.cream, band=P.m, lid=P.mDk, w=10, h=17},
    {body=P.red, band=P.yellow, lid=P.redDk, w=12, h=24},
    {body=P.mDk, band=P.mLt, lid=P.ink, w=14, h=19},
    {body=P.white, band=P.cyanDk, lid=P.gray, w=10, h=15},
  }
  for s, floor in ipairs({44, 78, 112}) do
    local x, i = 14, s
    while true do
      local t = tins[(i % #tins) + 1]
      if x + t.w > 145 then break end
      local top = floor - t.h
      fill(x, top, t.w, t.h, t.body)
      fill(x, top, t.w, 3, t.lid)
      fill(x, top + math.floor(t.h/2) - 1, t.w, 4, t.band)
      for y = top + 4, floor - 2 do px(x + 2, y, P.white) end
      box(x, top, t.w, t.h, P.ink)
      x = x + t.w + 2 + ((i * 7) % 5 == 0 and 8 or 0)
      i = i + 2
    end
  end
end)

-- customers share one shape so Unity can swap them; only colors and hair differ.
-- hair: "short", "long" (to the shoulders) or "bun"
local function drawCustomer(c)
  local cx = 210
  rbox(cx - 20, 68, 41, 76, c.shirt, P.ink, 9)
  line(cx - 12, 80, cx - 13, 140, c.shirtDk); line(cx + 12, 80, cx + 13, 140, c.shirtDk)
  for k = 0, 5 do px(cx - k, 69 + k, P.white); px(cx + k, 69 + k, P.white) end
  fill(cx - 1, 76, 2, 2, P.white); fill(cx - 1, 86, 2, 2, P.white)
  if c.hairStyle == "long" then fill(cx - 10, 52, 21, 16, P.ink); fill(cx - 9, 52, 19, 15, c.hair) end
  fill(cx - 3, 63, 7, 6, c.skinDk)
  if c.hairStyle == "bun" then disc(cx, 42, 5, P.ink); disc(cx, 42, 4, c.hair) end
  disc(cx, 54, 10, P.ink); disc(cx, 54, 9, c.skin)
  for dy = -10, 10 do for dx = -10, 10 do
    if dx*dx + dy*dy <= 90 and (dy <= -4 or (dy <= 2 and math.abs(dx) >= 7)) then px(cx + dx, 54 + dy, c.hair) end
  end end
  fill(cx - 4, 54, 1, 2, P.ink); fill(cx + 4, 54, 1, 2, P.ink)
  px(cx - 7, 58, P.blush); px(cx - 6, 58, P.blush); px(cx + 6, 58, P.blush); px(cx + 7, 58, P.blush)
  px(cx - 2, 59, P.ink); fill(cx - 1, 60, 3, 1, P.ink); px(cx + 2, 59, P.ink)
end

layer("Customer_Demo", function()
  drawCustomer{ shirt = P.shirt, shirtDk = P.shirtDk, skin = P.skin, skinDk = P.skinDk, hair = P.hair, hairStyle = "short" }
end)
-- extra looks for the queue; hidden here so the composed background only shows one customer
layer("Customer_2", function()
  drawCustomer{ shirt = P.m, shirtDk = P.mDk, skin = P.skin2, skinDk = P.skin2Dk, hair = P.hairBlack, hairStyle = "long" }
end).isVisible = false
layer("Customer_3", function()
  drawCustomer{ shirt = P.red, shirtDk = P.redDk, skin = P.skin, skinDk = P.skinDk, hair = P.blonde, hairStyle = "bun" }
end).isVisible = false
layer("Customer_4", function()
  drawCustomer{ shirt = P.orange, shirtDk = P.terraDk, skin = P.skin3, skinDk = P.skin3Dk, hair = P.grayLt, hairStyle = "short" }
end).isVisible = false

layer("Shop_Counter", function()
  fill(2, 137, 297, 5, P.counter)
  fill(2, 137, 297, 1, P.white)
  fill(2, 141, 297, 1, P.counterSh)
  fill(2, 142, 297, 23, P.mDk)
  for x = 6, 298, 8 do fill(x, 143, 1, 21, P.mDk2) end
  fill(2, 142, 297, 1, P.ink)
  -- tip jar
  fill(30, 125, 13, 12, P.glass); box(30, 125, 13, 12, P.ink); fill(31, 124, 11, 2, P.grayLt)
  for _, c in ipairs({{33,133},{36,131},{39,134},{35,135}}) do fill(c[1], c[2], 3, 2, P.yellow) end
end)

-- serving tray: finished snacks wait here until the drink is served
layer("Tray", function()
  fill(136, 132, 48, 5, P.ink)
  fill(137, 133, 46, 3, P.woodLt)
  fill(137, 135, 46, 1, P.wood)
  fill(133, 131, 4, 3, P.ink); fill(134, 132, 2, 1, P.woodLt)
  fill(183, 131, 4, 3, P.ink); fill(184, 132, 2, 1, P.woodLt)
end)

layer("Plant_Monstera", function()
  for _, l in ipairs({{270,108},{290,104},{280,95},{264,97},{293,117},{272,118}}) do line(282, 125, l[1], l[2], P.mDk2) end
  ellipse(270, 108, 9, 6, P.mDk); ellipse(290, 104, 8, 7, P.m); ellipse(280, 95, 7, 8, P.mDk)
  ellipse(264, 97, 7, 5, P.m); ellipse(293, 117, 5, 5, P.mDk); ellipse(272, 118, 6, 4, P.m)
  line(262, 108, 277, 108, P.mLt); line(284, 104, 296, 104, P.mLt); line(280, 89, 280, 101, P.mLt)
  for y = 124, 136 do local inset = math.floor((y - 124) / 4); fill(274 + inset, y, 17 - inset*2, 1, P.terracotta) end
  fill(273, 124, 19, 3, P.terraDk); box(273, 124, 19, 3, P.ink)
end)

-- tall enough for a pixel-font order of up to 7 short lines
layer("Order_Bubble", function()
  rbox(242, 4, 54, 44, P.white, P.ink, 3)
  for k = 0, 6 do fill(234 + k, 46 - k, 8 - k, 1, P.white); px(233 + k, 47 - k, P.ink) end
  line(234, 46, 242, 41, P.ink)
end)

layer("Patience_Track", function()
  rbox(242, 50, 54, 7, P.grayDk, P.ink, 1)
end)

layer("Patience_Fill", function()
  fill(243, 51, 52, 5, P.m); fill(243, 51, 52, 1, P.mLt)
end)

layer("Drink_Station_Background", function()
  fill(302, 2, 176, 134, P.station)
  for y = 2, 135 do
    local row = math.floor((y - 2) / 12)
    for x = 302, 477 do
      if (y - 2) % 12 == 0 or ((x - 302) + (row % 2) * 8) % 16 == 0 then px(x, y, P.stationLn) end
    end
  end
  fill(302, 136, 176, 28, P.counter)
  fill(302, 136, 176, 1, P.white)
  fill(302, 140, 176, 1, P.counterSh)
end)

for i, y in ipairs(SLOT_Y) do
  layer("Slot_" .. i, function()
    rfill(SLOT_X, y + 2, SLOT_W, SLOT_H, P.stationDk, 3)
    rbox(SLOT_X, y, SLOT_W, SLOT_H, P.white, P.ink, 3)
  end)
end

layer("Back_Button", function()
  rfill(BACK_X, BACK_Y + 2, SLOT_W, SLOT_H, P.mDk2, 3)
  rbox(BACK_X, BACK_Y, SLOT_W, SLOT_H, P.mPale, P.mDk, 3)
  for k = 0, 4 do fill(BACK_X + 8 + k, BACK_Y + 12 - k, 1, k*2 + 1, P.mDk) end
  fill(BACK_X + 13, BACK_Y + 11, 6, 3, P.mDk)
end)

layer("Path_Bar", function()
  rbox(364, 34, 111, 11, P.cream, P.stationDk, 2)
end)

layer("Cup_Fill_Demo", function()
  local function edges(y) local t = (y - 86) / 68; return math.floor(397 + 5*t + 0.5), math.floor(450 - 5*t + 0.5) end
  for y = 100, 153 do
    local lx, rx = edges(y)
    local c = (y < 117) and P.m or ((y < 120) and P.mLt or P.cream)
    if y >= 120 and ((y + math.floor(y / 3)) % 7 == 0) then c = P.creamDk end
    fill(lx + 1, y, rx - lx - 1, 1, c)
  end
  local lx, rx = edges(100); fill(lx + 1, 100, rx - lx - 1, 1, P.mLt)
  for _, ic in ipairs({{408,101},{426,105},{416,111},{434,99}}) do fill(ic[1], ic[2], 8, 7, P.ice); box(ic[1], ic[2], 8, 7, P.glass) end
end)

-- solid glass interior; Unity uses it as a Mask so cup layers follow the glass shape
layer("Cup_Mask", function()
  for y = 87, 153 do
    local t = (y - 86) / 68
    local lx, rx = math.floor(397 + 5*t + 0.5), math.floor(450 - 5*t + 0.5)
    fill(lx + 1, y, rx - lx - 1, 1, P.white)
  end
end)

layer("Cup", function()
  for y = 86, 154 do
    local t = (y - 86) / 68
    px(math.floor(397 + 5*t + 0.5), y, P.glass); px(math.floor(450 - 5*t + 0.5), y, P.glass)
  end
  for x = 402, 445 do px(x, 154, P.glass); px(x, 155, P.glass) end
  for x = 397, 450 do
    local a = (x - 423.5) / 26.5; local dy = math.floor(3 * math.sqrt(math.max(0, 1 - a*a)) + 0.5)
    px(x, 86 - dy, P.glass); px(x, 86 + dy, P.glass)
  end
  for y = 92, 148 do px(math.floor(400 + 5*(y-86)/68 + 0.5) + 1, y, P.white) end
end)

layer("Serve_Button", function()
  disc(SERVE_X, SERVE_Y + 2, SERVE_R, P.mDk2)
  disc(SERVE_X, SERVE_Y, SERVE_R, P.ink)
  disc(SERVE_X, SERVE_Y, SERVE_R - 1, P.m)
  for dx = -6, 0 do px(SERVE_X + dx - 1, SERVE_Y - 6 + math.floor(dx*dx/12), P.mLt) end
  for k = 0, 1 do line(SERVE_X - 5, SERVE_Y + k, SERVE_X - 2, SERVE_Y + 3 + k, P.white); line(SERVE_X - 2, SERVE_Y + 3 + k, SERVE_X + 5, SERVE_Y - 4 + k, P.white) end
end)

-- round red DUMP button left of the cup, same style as Serve, with a white trash can
layer("Dump_Button", function()
  local x, y, r = 380, SERVE_Y, SERVE_R
  disc(x, y + 2, r, P.redDk)
  disc(x, y, r, P.ink)
  disc(x, y, r - 1, P.redLt)
  for dx = -6, 0 do px(x + dx - 1, y - 6 + math.floor(dx * dx / 12), P.blush) end
  fill(x - 5, y - 5, 11, 2, P.white)              -- lid
  fill(x - 2, y - 7, 5, 2, P.white)               -- handle
  fill(x - 4, y - 2, 9, 8, P.white)               -- can
  for k = -2, 2, 2 do fill(x + k, y - 1, 1, 6, P.redLt) end
end)

layer("Prep_Station_Background", function()
  fill(2, 169, 297, 88, P.counter)
  fill(2, 254, 297, 3, P.counterSh)
end)

local SNACK_X = {40, 96, 152}
layer("Cold_Case", function()
  rbox(6, 171, 181, 35, P.grayLt, P.ink, 2)
  fill(9, 174, 175, 29, P.cyan)
  for x = 0, 175, 30 do for k = 0, 10 do px(14 + x + k, 176 + k, P.ice) end end
  fill(9, 199, 175, 4, P.white); fill(9, 199, 175, 1, P.grayLt)
  for _, x in ipairs(SNACK_X) do ellipse(x, 198, 13, 2, P.white); for i = x - 13, x + 13 do px(i, 200, P.grayLt) end end
end)

-- matcha roll cake, strawberry shortcake slice, dango skewer
layer("Cold_Snack_1", function()
  disc(40, 190, 7, P.ink); disc(40, 190, 6, P.m); disc(40, 190, 4, P.cream); disc(40, 190, 2, P.m)
end)
layer("Cold_Snack_2", function()
  for j = 0, 10 do fill(88 + math.floor(j*0.6), 186 + j, 17 - math.floor(j*0.6), 1, (j % 4 == 2) and P.red or P.cream) end
  disc(98, 184, 2, P.red)
end)
layer("Cold_Snack_3", function()
  line(140, 197, 165, 182, P.woodLt)
  disc(145, 193, 4, P.blush); disc(152, 189, 4, P.white); disc(159, 185, 4, P.m)
end)

layer("Hot_Case", function()
  rbox(6, 209, 181, 45, P.redDk, P.ink, 2)
  for y = 212, 250 do fill(9, y, 175, 1, (y < 222) and P.redLt or P.red) end
  for x = 14, 180, 24 do fill(x, 212, 12, 2, P.yellow) end
  fill(9, 247, 175, 4, P.white)
  for _, x in ipairs(SNACK_X) do ellipse(x, 246, 14, 2, P.white) end
end)

for i, x in ipairs(SNACK_X) do
  layer("Hot_Snack_" .. i, function()
    ellipse(x, 239, 12, 7, P.ink); ellipse(x, 239, 11, 6, P.orange); ellipse(x, 238, 9, 4, P.woodLt)
    ellipse(x, 238, 5, 3, P.ink); ellipse(x, 238, 4, 2, P.yellow)
    line(x - 9, 236, x - 5, 241, P.white); line(x + 5, 235, x + 9, 240, P.white)
  end)
end

layer("Microwave", function()
  rbox(192, 172, 102, 42, P.gray, P.ink, 2)
  rbox(196, 176, 66, 34, P.screen, P.ink, 1)
  for k = 0, 3 do line(204 + k*14, 180, 214 + k*14, 205, P.grayDk) end
  line(206, 181, 213, 193, P.grayLt)
  for r = 0, 2 do for c = 0, 2 do fill(268 + c*6, 178 + r*6, 4, 4, P.grayLt) end end
  disc(272, 204, 3, P.white); disc(284, 204, 3, P.white)
end)

layer("Toaster_Oven", function()
  rbox(192, 218, 102, 34, P.grayDk, P.ink, 2)
  rbox(198, 222, 72, 24, P.screen, P.ink, 1)
  for x = 202, 266, 2 do px(x, 226, ((x // 2) % 2 == 0) and P.orange or P.red); px(x, 241, ((x // 2) % 2 == 0) and P.red or P.orange) end
  fill(202, 233, 64, 1, P.grayLt)
  disc(282, 228, 4, P.ink); disc(282, 228, 3, P.white); disc(282, 240, 4, P.ink); disc(282, 240, 3, P.white)
  fill(198, 252, 6, 2, P.ink); fill(282, 252, 6, 2, P.ink)
end)

layer("Recipe_Panel", function()
  fill(302, 169, 176, 88, P.ink)
end)

-- both tabs drawn active-style; Unity tints the inactive one
layer("Tab_Menu", function() rbox(306, 171, 36, 14, P.cream, P.ink, 2) end)
layer("Tab_Recipe", function() rbox(343, 171, 46, 14, P.cream, P.ink, 2) end)

layer("Recipe_Paper", function()
  rbox(304, 183, 172, 72, P.cream, P.ink, 2)
  for y = 200, 250, 9 do fill(306, y, 168, 1, P.paperLn) end
  for y = 185, 252 do px(313, y, P.blush) end
end)

layer("Status_Bar", function()
  fill(0, 258, W, 12, P.black)
  disc(8, 264, 3, P.yellow); px(8, 263, P.white)
  fill(52, 261, 1, 6, P.grayDk); fill(100, 261, 1, 6, P.grayDk)
  disc(108, 264, 3, P.cream); px(108, 263, P.ink); px(108, 262, P.ink); px(109, 264, P.ink)
  for i = 0, 9 do disc(420 + i*6, 264, 2, P.grayDk) end
end)

-- one lit dot; Unity copies it along the row, one per happy customer
layer("Served_Dot", function() disc(420, 264, 2, P.m) end)

layer("Text_Preview", function()
  local labels = {"DAIRY", "FLAVORS", "SWEETENER", "MISC"}
  for i, s in ipairs(labels) do ctext(SLOT_X, SLOT_Y[i] + 10, SLOT_W, s, P.ink) end
  text(BACK_X + 24, BACK_Y + 10, "BACK", P.mDk)
  text(368, 37, "HOME", P.ink)
  ctext(SERVE_X - 12, 129, 24, "SERVE", P.ink)
  ctext(242, 10, 54, "SUNDAY", P.ink); ctext(242, 17, 54, "MATCHA", P.ink)
  ctext(242, 24, 54, "NO MILK", P.redDk); ctext(242, 31, 54, "+ DANISH", P.ink)
  ctext(306, 176, 36, "MENU", P.ink); ctext(343, 176, 46, "RECIPE", P.ink)
  text(318, 188, "SUNDAY MATCHA", P.mDk2)
  local items = {"CEREMONIAL MATCHA 100G", "WATER 4 OZ", "ANY MILK 6 OZ", "ANY SWEETENER 15G", "HOT DANISH"}
  for i, s in ipairs(items) do text(324, 202 + (i-1)*9, s, i == 1 and P.grayDk or P.ink) end
  text(14, 262, "120", P.cream)
  text(58, 262, "DAY 1", P.cream)
  text(114, 262, "9:30 AM", P.cream)
  text(372, 262, "SERVED", P.cream)
end)

-- order ticket over a customer waiting at pickup (hidden here; Unity places one per pickup spot):
-- paper with a pin, room for 3 short lines and a patience bar, tail pointing down at the head
layer("Ticket", function()
  rbox(14, 22, 41, 30, P.white, P.ink, 2)
  disc(34, 24, 1, P.red)
  for k = 0, 3 do fill(32 + k, 51 + k, 5 - 2 * k, 1, P.white); px(31 + k, 51 + k, P.ink); px(37 - k, 51 + k, P.ink) end
end).isVisible = false

-- title screen card (hidden here; Unity shows it over the dimmed shop at startup)
layer("Title_Card", function()
  rbox(120, 34, 240, 190, P.cream, P.ink, 4)
  rfill(125, 39, 230, 4, P.m, 1)
  -- matcha bowl with steam
  local cx, cy = 240, 66
  for dy = 0, 15 do for dx = -16, 16 do if dx*dx + dy*dy <= 256 then px(cx + dx, cy + dy, P.ink) end end end
  for dy = 0, 14 do for dx = -15, 15 do
    if dx*dx + dy*dy <= 225 then px(cx + dx, cy + dy, dx > 6 and P.terraDk or P.terracotta) end
  end end
  fill(cx - 7, cy + 16, 15, 2, P.ink)
  ellipse(cx, cy, 16, 4, P.ink); ellipse(cx, cy, 15, 3, P.m); ellipse(cx - 4, cy - 1, 6, 1, P.mLt)
  for k = -1, 1 do for j = 0, 9 do px(cx + k * 7 + (((j // 3) % 2 == 0) and 0 or 1), cy - 7 - j, P.grayLt) end end
  -- name
  local w = textWidth("MATCHA", 5)
  text(240 - w // 2 + 2, 96, "MATCHA", P.mDk2, 5)
  text(240 - w // 2, 94, "MATCHA", P.m, 5)
  ctext(120, 126, 240, "MANAGER", P.mDk2, 3)
end).isVisible = false

layer("Frame", function()
  box(1, 1, 299, 165, P.frame)
  box(301, 1, 178, 165, P.frame)
  box(1, 168, 299, 90, P.frame)
  box(301, 168, 178, 90, P.frame)
end)

spr:saveAs(out)

-- 8x8 icons for the drink tree buttons, exported one PNG per drink-tree name when run
-- with --script-param icondir=... ("a" = main color, "b" = accent, "k" = ink, "w" = white).
local SHAPES = {
  carton = {"..kkk...", ".kbbbk..", "kaaaaak.", "kabbbak.", "kabbbak.", "kaaaaak.", "kaaaaak.", "kkkkkkk."},
  cube   = {"........", ".kkkkkk.", ".kwaaak.", ".kaaabk.", ".kaaabk.", ".kabbbk.", ".kkkkkk.", "........"},
  pile   = {"........", "........", "...kk...", "..kaak..", ".kaabak.", "kaaaabbk", "kkkkkkkk", "........"},
  leaf   = {"......kk", "....kkak", "..kkaabk", ".kaabaak", "kaabaak.", "kabaak..", "kbkkk...", "k......."},
  drop   = {"...k....", "..kak...", "..kak...", ".kaaak..", "kawaaak.", "kawaaak.", ".kaaak..", "..kkk..."},
  icecube= {"........", ".kkkkkk.", "kwwaaabk", "kwaaaabk", "kaaaaabk", "kaaaabbk", "kbbbbbbk", ".kkkkkk."},
  bean   = {"........", "..kkkk..", ".kaabak.", "kaabaak.", "kaabaak.", "kabaak..", ".kkkk...", "........"},
  bar    = {"........", "kkkkkkk.", "kaakaak.", "kbbkbbk.", "kkkkkkk.", "kaakaak.", "kbbkbbk.", "kkkkkkk."},
  flower = {"...k....", "..kak...", "kkkakkk.", "kaabaak.", "kkkakkk.", "..kak...", "...k....", "........"},
  teabag = {"...k....", "...k....", "..kkk...", ".kaaak..", ".kaaak..", ".kabak..", ".kaaak..", "..kkk..."},
  pod    = {"......kk", ".....kak", "....kab.", "...kab..", "..kab...", ".kab....", "kab.....", "kk......"},
  bottle = {"...kk...", "...kk...", "..kaak..", ".kaaaak.", ".kwbbak.", ".kwbbak.", ".kaaaak.", "..kkkk.."},
  maple  = {"...k....", "k.kak.k.", "kakaakak", ".kaaaak.", "kaaaaaak", ".kkaakk.", "...kk...", "...k...."},
  bowl   = {"........", "........", "kkkkkkkk", "kaaaaaak", ".kbbbbk.", ".kbbbbk.", "..kkkk..", "..kkkk.."},
  box    = {"........", ".kkkkkk.", "kaaaaaak", "kkkkkkkk", "kbbwwbbk", "kbbbbbbk", "kbbbbbbk", "kkkkkkkk"},
  sparkle= {"...k....", "..kak...", ".kkakk..", "kaabaak.", ".kkakk..", "..kak...", "...k..k.", ".....kak"},
  more   = {"........", "...k....", "...kk...", "kkkkak..", "kaaaaak.", "kkkkak..", "...kk...", "...k...."},
  dot    = {"........", "........", "..kkkk..", ".kaaabk.", ".kaaabk.", ".kabbbk.", "..kkkk..", "........"},
  mug    = {"........", "kkkkkk..", "kbbbbkk.", "kwaaak.k", "kwaaak.k", "kaaaakk.", "kaaaak..", ".kkkk..."},
  kettle = {"..kkkk..", ".k....k.", ".kkkkkkk", "kwaaaakk", "kwaaaak.", "kaaaaabk", "kabbbbbk", ".kkkkkk."},
  bubbles= {".....kk.", "....kwak", "....kaak", ".kk..kk.", "kwak....", "kaak.k..", ".kk.kak.", ".....k.."},
  cream  = {"...kk...", "..kwak..", ".kkaakk.", "kwaaaaak", "kkkkkkkk", "kbbbbbbk", ".kbbbbk.", "..kkkk.."},
  foam   = {"..kk.kk.", ".kwakwak", "kaaaaaak", "kkkkkkkk", "kbbbbbbk", "kbbbbbbk", ".kbbbbk.", "..kkkk.."},
  coconut= {"........", ".k...k..", "kkkkkkkk", "kaaaaaak", "kbaaaabk", ".kbbbbk.", "..kkkk..", "........"},
  hex    = {"..kkkk..", ".kaaaak.", "kaakkaak", "kakbbkak", "kakbbkak", "kaakkaak", ".kaaaak.", "..kkkk.."},
  cookie = {"..kkkk..", ".kaabak.", "kaaaaaak", "kabaaaak", "kaaaabak", "kaaaaaak", ".kabaak.", "..kkkk.."},
}
local UBE, UBE_DK = C("a98bd0"), C("7d5ba6")
local ICONS = {
  -- folders
  {"Dairy", "carton", P.white, P.sky}, {"Non-Dairy", "carton", P.mPale, P.m},
  {"Drink Type", "mug", P.cream, P.wood}, {"Tea", "leaf", P.m, P.mDk}, {"Matcha", "bowl", P.m, P.terracotta},
  {"Chocolate", "bar", P.wood, P.woodDk}, {"Coffee", "bean", P.woodDk, P.hair},
  {"Sweeteners", "cube", P.white, P.grayLt}, {"Sugar", "pile", P.white, P.grayLt}, {"Syrup", "bottle", P.orange, P.yellow},
  {"Misc", "box", P.woodLt, P.wood},
  {"Toppings", "cream", P.white, P.redLt}, {"Garnish", "sparkle", P.yellow, P.orange}, {"Foam", "foam", P.white, P.grayLt},
  -- dairy
  {"Whole Milk", "carton", P.white, P.sky}, {"Half and Half", "carton", P.white, P.creamDk}, {"Heavy Cream", "carton", P.cream, P.yellow},
  {"Oat Milk", "carton", P.creamDk, P.woodLt}, {"Almond Milk", "carton", P.cream, P.wood}, {"Soy Milk", "carton", P.cream, P.mLt},
  {"Pistachio Milk", "carton", P.mPale, P.mLt},
  -- drink types
  {"Ceremonial Matcha", "pile", P.m, P.mDk}, {"Low-Grade Matcha", "pile", P.mLt, P.gray},
  {"Hojicha", "leaf", P.wood, P.woodDk}, {"Jasmine", "flower", P.white, P.yellow}, {"Earl Gray", "teabag", P.grayLt, P.gray},
  {"Dark Chocolate", "bar", P.woodDk, P.hair}, {"Milk Chocolate", "bar", P.woodLt, P.wood}, {"White Chocolate", "bar", P.cream, P.creamDk},
  {"Arabrica", "bean", P.wood, P.woodDk}, {"Robusta", "bean", P.woodDk, P.hair}, {"Decaf", "bean", P.woodLt, P.wood},
  {"Cold Brew", "bottle", P.woodDk, P.hair},
  -- sweeteners
  {"Cane Sugar", "pile", P.creamDk, P.woodLt}, {"White Sugar", "cube", P.white, P.grayLt}, {"Brown Sugar", "cube", P.woodLt, P.wood},
  {"Stevia", "leaf", P.mLt, P.m},
  {"Maple Syrup", "maple", P.orange, P.red}, {"Vanilla Syrup", "flower", P.cream, P.yellow}, {"Ube Syrup", "bottle", UBE, UBE_DK},
  -- misc
  {"Water", "drop", P.cyan, P.cyanDk}, {"Hot Water", "kettle", P.grayLt, P.gray}, {"Cold Water", "drop", P.sky, P.cyan},
  {"Sparkling Water", "bubbles", P.cyan, P.cyanDk}, {"Ice", "icecube", P.ice, P.cyan},
  -- toppings
  {"Coconut Flakes", "coconut", P.white, P.wood}, {"Caramel Honeycomb", "hex", P.yellow, P.orange},
  {"Caramel Drizzle", "drop", P.orange, P.terraDk}, {"Biscoff Crumbles", "cookie", P.woodLt, P.woodDk},
  {"Cheese Foam", "foam", P.cream, P.woodLt}, {"Sesame Foam", "foam", P.creamDk, P.wood}, {"Matcha Foam", "foam", P.mLt, P.cream},
  -- browser
  {"_More", "more", P.m, P.mDk}, {"_Default", "dot", P.grayLt, P.gray},
}
local icondir = app.params["icondir"]
if icondir then
  for _, ic in ipairs(ICONS) do
    local img = Image(8, 8, ColorMode.RGB)
    local colors = { a = ic[3], b = ic[4], k = P.ink, w = P.white }
    for y, row in ipairs(SHAPES[ic[2]]) do for x = 1, 8 do
      local c = colors[row:sub(x, x)]
      if c then img:drawPixel(x - 1, y - 1, c) end
    end end
    img:saveAs(app.fs.joinPath(icondir, ic[1] .. ".png"))
  end
end

-- Cup textures for solid ingredients, exported with --script-param cupdir=...:
-- Powder_Grain tiles over a powder layer (tinted darker in Unity), Ice_Cube floats in the drink.
local cupdir = app.params["cupdir"]
if cupdir then
  local function sprite(name, rows, colors)
    local img = Image(#rows[1], #rows, ColorMode.RGB)
    for y, row in ipairs(rows) do for x = 1, #row do
      local c = colors[row:sub(x, x)]
      if c then img:drawPixel(x - 1, y - 1, c) end
    end end
    img:saveAs(app.fs.joinPath(cupdir, name .. ".png"))
  end
  sprite("Powder_Grain", {"w.....", "...w..", ".....w", ".w....", "....w.", "..w..."}, { w = P.white })
  sprite("Ice_Cube", {".ooooo.", "ohhfffo", "ohffffo", "offfffo", "offffso", "offssso", ".ooooo."},
    { o = C("5ec8dd", 235), h = P.white, f = C("e4f8fc", 215), s = C("8fd9e8", 225) })
end

-- The same 3x5 font as a glyph atlas for Unity: white glyphs on transparent, one per
-- 8x8 cell with a clear border, so text rendering never picks up a neighbor's pixels.
-- plus PixelFont.json with each glyph's code point, cell and width.
local fontdir = app.params["fontdir"]
if fontdir then
  local keys = {}
  for k in pairs(G) do keys[#keys + 1] = k end
  table.sort(keys)
  local CELL, COLS = 8, 16
  local img = Image(COLS * CELL, math.ceil(#keys / COLS) * CELL, ColorMode.RGB)
  local white = app.pixelColor.rgba(255, 255, 255, 255)
  local entries = {}
  for i, k in ipairs(keys) do
    local g = G[k]
    local cx, cy = ((i - 1) % COLS) * CELL + 1, ((i - 1) // COLS) * CELL + 1
    for r = 1, 5 do for c = 1, #g[r] do
      if g[r]:sub(c, c) == "#" then img:drawPixel(cx + c - 1, cy + r - 1, white) end
    end end
    local code = k == "ELLIPSIS" and 0x2026 or string.byte(k)
    entries[#entries + 1] = string.format('{"code":%d,"x":%d,"y":%d,"w":%d}', code, cx, cy, #g[1])
  end
  img:saveAs(app.fs.joinPath(fontdir, "PixelFont.png"))
  local f = io.open(app.fs.joinPath(fontdir, "PixelFont.json"), "w")
  f:write('{"height":5,"glyphs":[' .. table.concat(entries, ",") .. ']}\n')
  f:close()
end

-- Finished drinks for Unity, exported with --script-param drinkdir=...: one 40x48 picture per
-- recipe (named after it), shown in place of the glass once the cup matches someone's order.
-- Each is a vessel (glass / mug / cup / bowl) with its liquid and toppings.
local DRINKS = {
  {"Sunday Matcha",     {vessel = "bowl", liquid = "6fa04a", foam = "a6cf7a", steam = true}},
  {"Hojicha Latte",     {vessel = "mug", liquid = "a8693f", foam = "e8d9b5", accent = "8a5432", steam = true}},
  {"Iced Matcha",       {vessel = "glass", layers = {{"6fa04a", 1}}, ice = true, straw = "b8423f"}},
  {"Mocha",             {vessel = "mug", liquid = "5e3720", cream = true, drizzle = "5e3720", accent = "5e3720"}},
  {"Matcha Latte",      {vessel = "cup", liquid = "8ab85a", art = "f3ead6", steam = true}},
  {"London Fog",        {vessel = "mug", liquid = "b59a7a", foam = "f3ead6", accent = "8a9096", steam = true}},
  {"Jasmine Milk Tea",  {vessel = "cup", liquid = "e0cf9a", art = "ffffff", steam = true}},
  {"Iced Coffee",       {vessel = "glass", layers = {{"5e3720", 0.6}, {"c8a27a", 0.4}}, ice = true, straw = "f3ead6"}},
  {"Hot Chocolate",     {vessel = "mug", liquid = "4a2c1d", cream = true, crumbs = "7a4a2e", accent = "b8423f"}},
  {"Cold Brew",         {vessel = "glass", layers = {{"3a2216", 1}}, ice = true, straw = "2a2230"}},
  {"Coconut Cold Brew", {vessel = "glass", layers = {{"3a2216", 0.6}, {"e8d9b5", 0.4}}, ice = true, flakes = "ffffff"}},
  {"Caramel Macchiato", {vessel = "glass", layers = {{"f3ead6", 0.5}, {"a8693f", 0.5}}, foam = "f7f4ec", drizzle = "b8702a"}},
  {"Honeycomb Latte",   {vessel = "mug", liquid = "c8a27a", foam = "f3ead6", honeycomb = true, accent = "e0a040"}},
  {"Biscoff Latte",     {vessel = "mug", liquid = "b5835a", cream = true, crumbs = "b5773a", accent = "a8693f"}},
  {"White Mocha",       {vessel = "mug", liquid = "f0e2c8", cream = true, drizzle = "d8c09a", accent = "e0d2b2"}},
  {"Skinny Latte",      {vessel = "cup", liquid = "d8c3a0", art = "ffffff", steam = true}},
  {"Breve",             {vessel = "cup", liquid = "a87a50", art = "f3ead6"}},
  {"Vienna Coffee",     {vessel = "glass", layers = {{"4a2c1d", 1}}, cream = true, top = 9}},
  {"Matcha Fizz",       {vessel = "glass", layers = {{"a6cf7a", 1}}, ice = true, bubbles = true, straw = "6fa04a"}},
  {"Pistachio Matcha",  {vessel = "cup", liquid = "c5d98a", art = "dcebc0", steam = true}},
  {"Matcha Cloud",      {vessel = "glass", layers = {{"f3ead6", 1}}, ice = true, foam = "a6cf7a", foamH = 8, straw = "6fa04a"}},
  {"Cheese Foam Tea",   {vessel = "glass", layers = {{"e0c070", 1}}, ice = true, foam = "fff3c8", foamH = 7}},
  {"Sesame Hojicha",    {vessel = "mug", liquid = "a8693f", foam = "e3d3b0", crumbs = "2a2230", accent = "40454a"}},
  {"Ube Latte",         {vessel = "glass", layers = {{"8a5fb0", 0.5}, {"e8d9b5", 0.5}}, ice = true, straw = "a98bd0"}},
  {"_Default",          {vessel = "cup", liquid = "a8693f", art = "f3ead6"}},
}
local drinkdir = app.params["drinkdir"]
if drinkdir then
  local DW, DH = 40, 48
  local pc = app.pixelColor
  local CLEAR = pc.rgba(0, 0, 0, 0)
  local function hexrgb(hex) return tonumber(hex:sub(1, 2), 16), tonumber(hex:sub(3, 4), 16), tonumber(hex:sub(5, 6), 16) end
  -- t > 0 mixes toward white, t < 0 toward black
  local function shade(hex, t)
    local r, g, b = hexrgb(hex)
    local function m(v) return math.floor((t >= 0 and v + (255 - v) * t or v * (1 + t)) + 0.5) end
    return pc.rgba(m(r), m(g), m(b), 255)
  end
  local function col(hex) return shade(hex, 0) end

  for _, entry in ipairs(DRINKS) do
    local name, d = entry[1], entry[2]
    local img = Image(DW, DH, ColorMode.RGB)
    local function px(x, y, c) if x >= 0 and y >= 0 and x < DW and y < DH then img:drawPixel(x, y, c) end end
    local function fill(x, y, w, h, c) for j = y, y + h - 1 do for i = x, x + w - 1 do px(i, j, c) end end end
    local function ell(cx, cy, rx, ry, c)
      for y = math.floor(cy - ry), math.ceil(cy + ry) do for x = math.floor(cx - rx), math.ceil(cx + rx) do
        local a, b = (x - cx) / rx, (y - cy) / ry
        if a * a + b * b <= 1 then px(x, y, c) end
      end end
    end
    local function lighten(x, y, t)
      if x < 0 or y < 0 or x >= DW or y >= DH then return end
      local v = img:getPixel(x, y)
      if pc.rgbaA(v) == 0 then return end
      local function m(c) return math.floor(c + (255 - c) * t + 0.5) end
      px(x, y, pc.rgba(m(pc.rgbaR(v)), m(pc.rgbaG(v)), m(pc.rgbaB(v)), 255))
    end
    local function steam(y0)
      for _, sx in ipairs({13, 19, 25}) do
        for k = 0, 6 do px(sx + ((k // 2) % 2), y0 - k - (sx == 19 and 1 or 0), P.grayLt) end
      end
    end
    local function dots(color, spots) for _, s in ipairs(spots) do px(s[1], s[2], col(color)) end end
    local function zigzag(color, x0, x1, y) for x = x0, x1 do px(x, y + ((x // 2) % 2), col(color)) end end
    -- whipped cream piled on top, its base at y
    local function cream(cx, y, rx)
      ell(cx, y - 3, rx, 4.5, P.ink); ell(cx, y - 3, rx - 1, 3.5, P.white)
      ell(cx + 2, y - 2, rx - 4, 1.5, P.cream)
      px(math.floor(cx), y - 8, P.ink); px(math.floor(cx), y - 7, P.white); px(math.floor(cx) + 1, y - 8, P.ink)
    end

    if d.vessel == "glass" then
      local L, R, T, B = 11, 28, 8, 45
      local top = d.top or 11
      fill(L + 1, T + 1, R - L - 1, top - T - 1, C("dff3f8", 110)) -- empty glass above the drink
      local total, y = B - top, B - 1
      for i, layer in ipairs(d.layers) do
        local h = i == #d.layers and y - top + 1 or math.floor(total * layer[2] + 0.5)
        for j = 0, h - 1 do fill(L + 1, y - j, R - L - 1, 1, j == h - 1 and shade(layer[1], 0.3) or col(layer[1])) end
        y = y - h
      end
      local fh = d.foam and (d.foamH or 4) or 0
      if d.foam then
        for j = 0, fh - 1 do fill(L + 1, top + j, R - L - 1, 1, j == 0 and shade(d.foam, 0.4) or col(d.foam)) end
        for x = L + 1, R - 1, 3 do px(x, top - 1, col(d.foam)) end
      end
      if d.ice then
        for _, c in ipairs({{13, 2}, {20, 6}, {14, 12}, {21, 17}, {15, 23}}) do
          local cx, cy = c[1], top + fh + c[2]
          if cy + 4 < B then
            fill(cx, cy, 5, 5, C("8fd9e8")); fill(cx + 1, cy + 1, 3, 3, C("e4f8fc")); px(cx + 1, cy + 1, P.white)
          end
        end
      end
      if d.bubbles then dots("ffffff", {{14, 30}, {23, 26}, {17, 38}, {25, 35}, {20, 21}, {15, 19}, {24, 41}, {18, 33}}) end
      if d.flakes then dots(d.flakes, {{13, top}, {16, top - 1}, {19, top}, {22, top - 1}, {25, top}, {18, top + 1}, {24, top + 1}}) end
      if d.drizzle then
        zigzag(d.drizzle, L + 1, R - 1, top)
        for j = 0, 5 do px(L + 2, top + fh + j, col(d.drizzle)); px(R - 3, top + fh + j - 2, col(d.drizzle)) end
      end
      if d.straw then
        for sy = 0, top + 8 do
          local sx = 26 - sy // 5
          px(sx, sy, col(d.straw)); px(sx + 1, sy, shade(d.straw, -0.3))
        end
      end
      -- the glass itself: pale walls, a rim and a highlight down the left side
      fill(L, T, 1, B - T, C("8fc9da")); fill(R, T, 1, B - T, C("8fc9da")); fill(L + 1, B, R - L - 1, 1, C("8fc9da"))
      fill(L, T, R - L + 1, 1, C("bdeffd"))
      for hy = top + 1, B - 3 do if hy % 6 ~= 0 then lighten(L + 2, hy, 0.45) end end
      if d.cream then cream(19.5, T + 1, 9) end
    elseif d.vessel == "mug" then
      local L, R, T, B = 8, 29, 18, 44
      local M, MS = P.cream, P.creamDk
      fill(29, 23, 6, 15, P.ink); fill(30, 24, 4, 13, M); fill(31, 26, 2, 9, CLEAR)
      fill(L, T, R - L + 1, B - T + 1, P.ink); fill(L + 1, T + 1, R - L - 1, B - T - 1, M)
      fill(R - 4, T + 1, 3, B - T - 1, MS)
      px(L, B, CLEAR); px(R, B, CLEAR)
      if d.accent then fill(L + 1, T + 13, R - L - 1, 2, col(d.accent)) end
      ell(18.5, T, 10.5, 3.5, P.ink); ell(18.5, T, 9.5, 2.6, col(d.liquid))
      ell(18.5, T + 1, 8, 1, shade(d.liquid, -0.15))
      if d.foam then ell(18.5, T - 0.3, 6.5, 1.6, col(d.foam)) end
      local ty = T
      if d.cream then cream(18.5, T, 9); ty = T - 5 end
      if d.drizzle then zigzag(d.drizzle, 12, 25, ty - 1) end
      if d.crumbs then dots(d.crumbs, {{13, ty}, {16, ty - 1}, {19, ty}, {22, ty - 1}, {24, ty + 1}, {17, ty + 1}}) end
      if d.honeycomb then
        fill(20, T - 3, 6, 4, P.ink); fill(21, T - 2, 4, 2, P.yellow); px(22, T - 2, P.orange); px(23, T - 1, P.orange)
      end
      if d.steam then steam(d.cream and T - 9 or T - 3) end
    elseif d.vessel == "cup" then
      local W, WS = P.white, P.grayLt
      ell(19.5, 42, 18, 3.5, P.ink); ell(19.5, 42, 17, 2.5, P.creamDk)
      fill(33, 27, 5, 8, P.ink); fill(34, 28, 3, 6, W); fill(35, 29, 1, 4, CLEAR)
      for y = 24, 39 do
        local hw = 13 - math.max(0, y - 34)
        fill(20 - hw, y, 2 * hw, 1, P.ink)
        if y < 39 then fill(21 - hw, y, 2 * hw - 2, 1, W); fill(16 + hw, y, 3, 1, WS) end
      end
      ell(19.5, 24, 13, 3.5, P.ink); ell(19.5, 24, 12, 2.6, col(d.liquid))
      ell(19.5, 25, 10, 1, shade(d.liquid, -0.15))
      if d.art then ell(19.5, 24, 4, 1.3, col(d.art)); px(17, 23, col(d.art)); px(22, 23, col(d.art)) end
      if d.steam then steam(20) end
    elseif d.vessel == "bowl" then
      local T = 26
      for y = T, T + 13 do
        local hw = math.floor(15 * math.sqrt(math.max(0, 1 - ((y - T) / 14) ^ 2)))
        if hw > 0 then
          fill(20 - hw, y, 2 * hw, 1, P.ink)
          fill(21 - hw, y, 2 * hw - 2, 1, P.terracotta)
          fill(20 + hw - 5, y, 4, 1, P.terraDk)
        end
      end
      fill(14, T + 13, 12, 3, P.ink); fill(15, T + 13, 10, 2, P.terraDk)
      ell(19.5, T, 15.5, 3, P.ink); ell(19.5, T, 14.5, 2, col(d.liquid))
      ell(17, T, 7, 1, col(d.foam)); dots(d.foam, {{24, T - 1}, {26, T}, {11, T}})
      if d.steam then steam(T - 4) end
    end
    img:saveAs(app.fs.joinPath(drinkdir, name .. ".png"))
  end
end

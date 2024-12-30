PREFIX_REQUIRED = ''
NOBORDER_MODE = True

DESCRIPTION = '''\
visualizer_wc24 v1.0a_public by aCat

For the exact readme see 'visualizer_wc24.md'

WARNING: program tries to use 'UbuntuMono-R.ttf', if not present into your system change font in the program constant. '''

import os, sys, re
import argparse
from PIL import Image, ImageDraw, ImageFont


IMAGES_DIR_OUT = "imgs"
FONT = 'arial.ttf'

CELL_SIZE = 50
CELL_BORDER = 2
LPANEL_WIDTH=400
RPANEL_WIDTH=400
BOARD_MARGIN_X = 10+LPANEL_WIDTH
BOARD_MARGIN_Y = CELL_SIZE
if NOBORDER_MODE:
    BOARD_MARGIN_X += CELL_SIZE
    BOARD_MARGIN_Y += CELL_SIZE

MAP_WIDTH=0
MAP_HEIGHT=0
COORDSMODE='H'

def cellx(x): return int((BOARD_MARGIN_X + x*CELL_SIZE))
def celly(y): return int((BOARD_MARGIN_Y + y*CELL_SIZE))


COL_PLAYERS=['#eb5200', '#2b92bc']
COL_WALL = '#000000'
COL_NONWALL = '#7e172a' #'#27cfec'
COL_RES = '#00ff00'

TEXTSIZE_RES = (int)(CELL_SIZE*0.8)
TEXT_RES_BORDER = 2


def drawTextCellCenter(draw, x, y, msg, font, col, outline='black', border=0):
    msg = msg.replace('\\n', '\n')
    cx = cellx(x)+CELL_SIZE/2
    cy = celly(y)+CELL_SIZE/2
    _, _, w, h = draw.textbbox((0, 0), msg, font=font)
    draw.text( (cx-w/2, cy-h/2 ), msg, font=font, fill=col, stroke_fill=outline, stroke_width=border)

def drawTextCellPos(draw, x, y, pos, msg, font, col, outline='black', border=0):
    msg = msg.replace('\\n', '\n')
    cx = cellx(x)+CELL_SIZE/2
    if "L" in pos: cx -= CELL_SIZE/3
    if "R" in pos: cx += CELL_SIZE/3
    cy = celly(y)+CELL_SIZE/2
    if "T" in pos: cy -= CELL_SIZE/3
    if "B" in pos: cy += CELL_SIZE/3
    _, _, w, h = draw.textbbox((0, 0), msg, font=font)
    draw.text( (cx-w/2, cy-h/2 ), msg, font=font, fill=col, stroke_fill=outline, stroke_width=border)

def drawPanelText(draw, tup, which ):
    size = 0.5 if which == 1 else 0.5 # 0.3
    font = ImageFont.truetype(FONT, int(CELL_SIZE*size))
    line, msg, col = tup
    nx = 10 + which*cellx(MAP_WIDTH)
    ny = celly(0) + (line*size)*CELL_SIZE 
    draw.text( (nx, ny ), msg, font=font, fill=col)

def drawCellBit(draw, x, y, pos, col):
    r = int(CELL_SIZE/6)
    cx = cellx(x)+CELL_SIZE/2
    if "L" in pos: cx -= CELL_SIZE/3
    if "R" in pos: cx += CELL_SIZE/3
    cy = celly(y)+CELL_SIZE/2
    if "T" in pos: cy -= CELL_SIZE/3
    if "B" in pos: cy += CELL_SIZE/3
    draw.ellipse((cx-r, cy-r, cx+r, cy+r), fill=col, outline='black', width=0)


class Cell:
    def __init__(self, x, y, params): # "E" / "W" / "A" / "B" / "C" / "D" / "RBSHT,01,NSWE,NSWE"
        #print(params)
        #self.id = params[0] 
        self.x = x
        self.y = y
        self.type = params[0]
        self.iswall = self.type in "W"
        self.isres = len(params)<2 and self.type in "ABCD"
        self.player = -1 if len(params)<2 else int(params[1])
        self.dir = "Q" if len(params)<3 else params[2]
        self.dirFrom = "Q" if len(params)<4 else params[3]

    def drawFrom(self, draw):
        if self.iswall or self.isres or self.type == 'E' or self.type == 'R': return
        x=self.x; y=self.y
        cx = cellx(x)+CELL_SIZE/2
        cy = celly(y)+CELL_SIZE/2
        r=int(CELL_SIZE*0.15)
        if self.dirFrom=="N": pol = [(cx,celly(y)+r), (cx-r,celly(y)-r), (cx+r,celly(y)-r)]
        if self.dirFrom=="S": pol = [(cx,celly(y+1)-r), (cx-r,celly(y+1)+r), (cx+r,celly(y+1)+r)]
        if self.dirFrom=="W": pol = [(cellx(x)+r,cy), (cellx(x)-r,cy-r), (cellx(x)-r,cy+r)]
        if self.dirFrom=="E": pol = [(cellx(x+1)-r,cy), (cellx(x+1)+r,cy-r), (cellx(x+1)+r,cy+r)]
        draw.polygon(pol, fill= COL_PLAYERS[self.player], outline ="black", width=CELL_BORDER) 

    def draw(self, draw):
        x=self.x; y=self.y
        draw.rectangle([cellx(x), celly(y), cellx(x+1), celly(y+1)], fill= COL_WALL if self.iswall else COL_NONWALL, outline ="black", width=CELL_BORDER) 
        if self.iswall: return
        if self.isres:
            drawTextCellCenter(draw, x, y, self.type, ImageFont.truetype(FONT, TEXTSIZE_RES), COL_RES, 'black', TEXT_RES_BORDER)
            return
        cx = cellx(x)+CELL_SIZE/2
        cy = celly(y)+CELL_SIZE/2
        if self.type == 'R': 
            r = int(CELL_SIZE*0.4)
            draw.ellipse((cx-r, cy-r, cx+r, cy+r), fill=COL_PLAYERS[self.player], outline='black', width=CELL_BORDER)
            return
        if self.type == 'B': 
            r = int(CELL_SIZE*0.3)
            draw.rectangle([cx-r, cy-r, cx+r, cy+r], fill= COL_PLAYERS[self.player], outline ="black", width=CELL_BORDER) 
            return
        if self.type == 'T': 
            r = int(CELL_SIZE*0.4)
            if self.dir=="N": pol = [(cx,cy-r), (cx+r,cy+r), (cx-r,cy+r)]
            if self.dir=="S": pol = [(cx,cy+r), (cx+r,cy-r), (cx-r,cy-r)]
            if self.dir=="W": pol = [(cx-r,cy), (cx+r,cy-r), (cx+r,cy+r)]
            if self.dir=="E": pol = [(cx+r,cy), (cx-r,cy-r), (cx-r,cy+r)]
            draw.polygon(pol, fill= COL_PLAYERS[self.player], outline ="black", width=CELL_BORDER) 
            return
        if self.type == 'H': 
            r = int(CELL_SIZE*0.4)
            if self.dir=="N": pol = [(cx,cy), (cx+r,cy-r), (cx+r,cy+r), (cx-r,cy+r), (cx-r,cy-r)]
            if self.dir=="E": pol = [(cx,cy), (cx+r,cy+r), (cx-r,cy+r), (cx-r,cy-r), (cx+r,cy-r)]
            if self.dir=="S": pol = [(cx,cy), (cx-r,cy+r), (cx-r,cy-r), (cx+r,cy-r), (cx+r,cy+r)]
            if self.dir=="W": pol = [(cx,cy), (cx-r,cy-r), (cx+r,cy-r), (cx+r,cy+r), (cx-r,cy+r)]
            draw.polygon(pol, fill= COL_PLAYERS[self.player], outline ="black", width=CELL_BORDER) 
            return
        if self.type == 'S': 
            r = int(CELL_SIZE*0.4)
            if self.dir=="N": rectBig, rectSmall = [cx-r, cy, cx+r, cy+r], [cx-r/2, cy-r, cx+r/2, cy]
            if self.dir=="S": rectBig, rectSmall = [cx-r, cy-r, cx+r, cy], [cx-r/2, cy, cx+r/2, cy+r]
            if self.dir=="W": rectBig, rectSmall = [cx, cy-r, cx+r, cy+r], [cx-r, cy-r/2, cx, cy+r/2]
            if self.dir=="E": rectBig, rectSmall = [cx-r, cy-r, cx, cy+r], [cx, cy-r/2, cx+r, cy+r/2]
            draw.rectangle(rectBig, fill= COL_PLAYERS[self.player], outline ="black", width=CELL_BORDER) 
            draw.rectangle(rectSmall, fill= COL_PLAYERS[self.player], outline ="black", width=CELL_BORDER) 
            return

FRAME = None
class Frame:
    def __init__(self, name, only):
        global MAP_HEIGHT, MAP_WIDTH, COORDSMODE
        self.name = name
        self.tosave = only in name
        self.cells = [[Cell(x, y, "W") for y in range(14)] for x in range(26)] # [x][y]
        self.text = [[],[]]
        self.textline = [0,0]
        self.res=[]
        self.inc=[]
        self.size=[]
        self.masksbit = [] # tuples: (pos, col, 101100011011..bitmask)
        self.masksbitREV = [] # tuples: (pos, col, 101100011011..bitmask) # but bits in reverse order
        self.maskscell = [] # tuples: (pos, col, x, y)
        self.maskslist = [] # tuples: (pos, col, [cid])
        self.masksbittext = [] # tuples: (pos, col, [text00 text10..])
        self.maskscelltext = [] # tuples: (pos, col, x, y, text)

    def addPanelText(self, line, text, col, which):
        if line == -1: 
            line = self.textline[which]
            self.textline[which] += text.count('\\n') + 1
        self.text[which].append( (line, text.replace('\\n', '\n'), col) )

    def drawPlayerInfo(self, draw, pid):
        strwidth=0
        if not self.res: return
        rfont = ImageFont.truetype(FONT, int(CELL_SIZE*0.5))
        cx = BOARD_MARGIN_X + (CELL_SIZE*MAP_WIDTH) / 2
        dx = cx + (-1 if pid == 0 else 1) * (CELL_SIZE*MAP_WIDTH) / 4;
        ny = 1
        msg = ''.join([s.rjust(4) for s in self.res[pid*4+0:pid*4+4]])
        _, _, w, h = draw.textbbox((0, 0), msg, font=rfont)
        draw.text( (dx-w/2, ny ), msg, font=rfont, fill=COL_PLAYERS[pid], stroke_fill='black', stroke_width=strwidth)
        if self.size:
            sizefont = ImageFont.truetype(FONT, int(CELL_SIZE*0.95))
            _, _, ws, hs = draw.textbbox((0, 0), self.size[pid*2], font=sizefont)
            dxs = cx + (-1 if pid == 0 else 1) * 1.5*CELL_SIZE;
            draw.text( (dxs-ws/2, ny ), self.size[pid*2], font=sizefont, fill=COL_PLAYERS[pid], stroke_fill='black', stroke_width=strwidth)
            _, _, ws, hs = draw.textbbox((0, 0), self.size[1+pid*2], font=rfont)
            dxs = cx + (-1 if pid == 0 else 1) * 3*CELL_SIZE;
            draw.text( (dxs-ws/2, ny+CELL_SIZE/2-hs/2 ), self.size[1+pid*2], font=rfont, fill=COL_PLAYERS[pid], stroke_fill='black', stroke_width=strwidth)
        if self.inc: 
            ny += CELL_SIZE*0.5
            msg = ''.join([s.rjust(4) for s in self.inc[pid*4+0:pid*4+4]])
            draw.text( (dx-w/2, ny ), msg, font=rfont, fill=COL_PLAYERS[pid], stroke_fill='black', stroke_width=strwidth)
            draw.text( (dx-w/2-10, ny ), '+', font=rfont, fill=COL_PLAYERS[pid], stroke_fill='black', stroke_width=strwidth)

    def drawCoords(self, draw):
        #print(COORDSMODE)
        if COORDSMODE == 'H':
            font = ImageFont.truetype(FONT, int(CELL_SIZE*0.6))
            if NOBORDER_MODE:
                for x in range(0,MAP_WIDTH): drawTextCellCenter(draw, x, -1, str(x), font, 'black')
                for y in range(0,MAP_HEIGHT): drawTextCellCenter(draw, -1, y, str(y), font, 'black')
            else:
                for x in range(1,MAP_WIDTH-1): drawTextCellCenter(draw, x, 0, str(x), font, 'white')
                for y in range(1,MAP_HEIGHT-1): drawTextCellCenter(draw, 0, y, str(y), font, 'white')
        if COORDSMODE == 'C':
            font = ImageFont.truetype(FONT, int(CELL_SIZE*0.3))
            for x in range(1,MAP_WIDTH-1): 
                for y in range(1,MAP_HEIGHT-1): drawTextCellPos(draw, x, y, 'B', str(x)+','+str(y), font, 'white')

    def drawText(self, draw):
        for tup in self.text[0]: drawPanelText(draw, tup, 0)
        for tup in self.text[1]: drawPanelText(draw, tup, 1)
        #print (self.text)

    def drawMasks(self, draw):
        for (pos, col, bitmask) in self.masksbitREV:
            #print(pos, col, bitmask)
            for y in range(MAP_HEIGHT):
                for x in range(MAP_WIDTH):
                    if bitmask[MAP_WIDTH*MAP_HEIGHT- (y*MAP_WIDTH+x) -1 ] == '1':
                        drawCellBit(draw, x, y, pos, col)
        for (pos, col, bitmask) in self.masksbit:
            #print(pos, col, bitmask)
            for y in range(MAP_HEIGHT):
                for x in range(MAP_WIDTH):
                    if bitmask[y*MAP_WIDTH+x] == '1':
                        drawCellBit(draw, x, y, pos, col)
        for (pos, col, x, y) in self.maskscell:
            #print(pos, col, x, y)
            drawCellBit(draw, int(x), int(y), pos, col)
        for (pos, col, cids) in self.maskslist:
            #print(pos, col, cids)
            for cid in cids:
                drawCellBit(draw, int(cid%MAP_WIDTH), int(cid/MAP_WIDTH), pos, col)
        for (pos, col, x, y, text) in self.maskscelltext:
            #print(pos, col, x, y, text)
            drawTextCellPos(draw, int(x), int(y), pos, text, ImageFont.truetype(FONT, int(CELL_SIZE*0.3)), col)
        for (pos, col, texts) in self.masksbittext:
            # print(pos, col, texts)
            for y in range(MAP_HEIGHT):
                for x in range(MAP_WIDTH):
                    drawTextCellPos(draw, int(x), int(y), pos, texts[y*MAP_WIDTH+x], ImageFont.truetype(FONT, int(CELL_SIZE*0.3)), col)

    def drawAndSave(self):
        img = Image.new('RGBA', (BOARD_MARGIN_X+MAP_WIDTH*CELL_SIZE+RPANEL_WIDTH, BOARD_MARGIN_Y+MAP_HEIGHT*CELL_SIZE+10), '#ffffff')  # todo update values
        draw = ImageDraw.Draw(img)

        for y in range(MAP_HEIGHT):
            for x in range(MAP_WIDTH):
                self.cells[x][y].draw(draw)
        for y in range(MAP_HEIGHT):
            for x in range(MAP_WIDTH):
                self.cells[x][y].drawFrom(draw)

        self.drawPlayerInfo(draw, 0)
        self.drawPlayerInfo(draw, 1)

        self.drawCoords(draw)
        self.drawMasks(draw)

        self.drawText(draw)

        img = img.convert('RGBA')
        img.save(f"{IMAGES_DIR_OUT}/{self.name}.png","PNG")
        print(f"Saved {IMAGES_DIR_OUT}/{self.name}.png")
        return 
        


def preprocessLine(line):
    line = line.split('//',1)[0].split('?>',1)[0].split('##',1)[0].strip()
    if line.startswith(PREFIX_REQUIRED): return line[len(PREFIX_REQUIRED):] 
    else: return ''

def applyCommand(line, only):
    global FRAME, MAP_HEIGHT, MAP_WIDTH, COORDSMODE
    cmd = re.split(r'\s+', line, 1)
    #print(cmd)

    if cmd[0] == 'FRAME':
        if FRAME and FRAME.tosave: FRAME.drawAndSave()
        FRAME = Frame('default' if len(cmd) < 2 else cmd[1], only)
        #print('frame', FRAME.name)
        return
    
    if FRAME and not FRAME.tosave: return
    elif cmd[0] == 'INIT':
        l = list(map(int,re.split(r'\s+', cmd[1])))
        MAP_WIDTH, MAP_HEIGHT = l[0], l[1]
        #print(MAP_WIDTH, MAP_HEIGHT)
    elif cmd[0] == 'COORDS': COORDSMODE = cmd[1]
    elif cmd[0] == 'CELL':
        args = re.split(r'\s+', cmd[1], 2)
        c = Cell(int(args[0]), int(args[1]), args[2])
        FRAME.cells[c.x][c.y] = c
    elif cmd[0] == 'BOARD':
        args = re.split(r'\s+', cmd[1])
        for y in range(MAP_HEIGHT):
            for x in range(MAP_WIDTH):
                FRAME.cells[x][y] = Cell(x, y, args[y*MAP_WIDTH+x])
    elif cmd[0] == 'RES': FRAME.res =  re.split(r'\s+', cmd[1])
    elif cmd[0] == 'INC': FRAME.inc =  re.split(r'\s+', cmd[1])
    elif cmd[0] == 'SIZE': FRAME.size =  re.split(r'\s+', cmd[1])

    elif cmd[0] == 'TEXTL':
        if(len(cmd)>1): FRAME.addPanelText(-1, cmd[1], 'black', 0)
    elif cmd[0] == 'TEXTR':
        if(len(cmd)>1):FRAME.addPanelText(-1, cmd[1], 'black', 1)
    elif cmd[0] == 'TEXTLC':
        args = re.split(r'\s+', cmd[1], 1)
        if(len(args)>1): FRAME.addPanelText(-1, args[1], args[0], 0)
    elif cmd[0] == 'TEXTRC':
        args = re.split(r'\s+', cmd[1], 1)
        if(len(args)>1): FRAME.addPanelText(-1, args[1], args[0], 1)
    elif cmd[0] == 'TEXTL0': 
        if(len(cmd)>1): FRAME.addPanelText(-1, cmd[1], COL_PLAYERS[0], 0)
    elif cmd[0] == 'TEXTL1': 
        if(len(cmd)>1): FRAME.addPanelText(-1, cmd[1], COL_PLAYERS[1], 0)
    elif cmd[0] == 'TEXTR0': 
        if(len(cmd)>1): FRAME.addPanelText(-1, cmd[1], COL_PLAYERS[0], 1)
    elif cmd[0] == 'TEXTR1': 
        if(len(cmd)>1): FRAME.addPanelText(-1, cmd[1], COL_PLAYERS[1], 1)
    # elif cmd[0] == 'TEXTL':
    #     args = re.split(r'\s+', cmd[1], 1)
    #     FRAME.addPanelText(int(args[0]), args[1], 'black', 1)

    elif cmd[0] == 'MASKB': 
        args = re.split(r'\s+', cmd[1])
        FRAME.masksbit.append( (args[0], args[1], args[2]) )
    elif cmd[0] == 'MASKB!!': 
        args = re.split(r'\s+', cmd[1])
        FRAME.masksbitREV.append( (args[0], args[1], args[2]) )
    elif cmd[0] == 'MASKC':
        args = re.split(r'\s+', cmd[1])
        FRAME.maskscell.append( (args[0], args[1], args[2], args[3] ) )
    elif cmd[0] == 'MASKL': 
        args = re.split(r'\s+', cmd[1], 2)
        FRAME.maskslist.append( (args[0], args[1], [] if len(args)<3 else list(map(int,re.split(r'\s+', args[2]))) ) )
    elif cmd[0] == 'MASKTC':
        args = re.split(r'\s+', cmd[1])
        FRAME.maskscelltext.append( (args[0], args[1], args[2], args[3], args[4] ) )
    elif cmd[0] == 'MASKTB': 
        args = re.split(r'\s+', cmd[1], 2)
        FRAME.masksbittext.append( (args[0], args[1], [] if len(args)<3 else list(re.split(r'\s+', args[2]))) )
   
    else:
        pass
        if cmd: print('WARNING: Unknown command:', cmd[0], '' if len(cmd)<2 else cmd[1])
        

if __name__ == "__main__":
    parser = argparse.ArgumentParser(formatter_class=argparse.RawDescriptionHelpFormatter,description=DESCRIPTION)
    parser.add_argument("-d", "--dir",  default=IMAGES_DIR_OUT, help=f"directory to generate images, default is '{IMAGES_DIR_OUT}'")
    parser.add_argument("-o", "--only", default='', help="generate images only if their titles contain the given string (use '' to still generate everything)")
    parser.add_argument("-f", "--file", default='', help="generate images from given file input")
    args = parser.parse_args()

    IMAGES_DIR_OUT = args.dir
    os.makedirs(IMAGES_DIR_OUT, exist_ok=True)
    FRAME = Frame('default', str(args.only))
    if args.file:
        print(args.file)
        with open(args.file) as f:
            for line in f:
                if not line: break
                line = preprocessLine(line)
                if line=='END': break
                if not line: continue
                applyCommand(line, str(args.only))
    else:
        for line in sys.stdin:
            if not line: break
            line = preprocessLine(line)
            if line=='END': break
            if not line: continue
            applyCommand(line, str(args.only))

        #print ('>', line, '['+str(args.only)+']')
    applyCommand('FRAME', '')
    #print ('PYEND')
    sys.exit(0)


from pathlib import Path
from PIL import Image, ImageDraw
root = Path(__file__).resolve().parents[1] / 'src/BT583Printer/Assets'
root.mkdir(parents=True, exist_ok=True)
n = 1024
im = Image.new('RGBA', (n,n), (0,0,0,0))
d = ImageDraw.Draw(im)
d.rounded_rectangle((24,24,1000,1000), radius=232, fill='#2563eb')
d.rounded_rectangle((245,150,779,460), radius=36, fill='#dbeafe')
d.rounded_rectangle((175,350,849,740), radius=92, fill='#ffffff')
d.rounded_rectangle((259,540,765,818), radius=26, fill='#172b50')
d.rounded_rectangle((295,572,729,874), radius=24, fill='#ffffff')
d.rounded_rectangle((350,643,674,674), radius=15, fill='#2563eb')
d.rounded_rectangle((350,717,580,748), radius=15, fill='#94b9ff')
d.ellipse((716,415,762,461), fill='#22c9a0')
im.resize((256,256),Image.Resampling.LANCZOS).save(root/'app-icon.png')
im.save(root/'app.ico', sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])

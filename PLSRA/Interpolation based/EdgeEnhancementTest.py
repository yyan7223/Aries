from PIL import Image
from skimage.metrics import structural_similarity
from skimage.metrics import peak_signal_noise_ratio
from numpy import asarray
import time

width = 2400 
height = 1080

#https://hristog.github.io/running_python_scripts_on_your_android_device.html
#Image.NEAREST (0), Image.LANCZOS (1), Image.BILINEAR (2), Image.BICUBIC (3), Image.BOX (4), Image.HAMMING (5)

originalImage = Image.open("AssetsVikingVillage_HR.png")
originalImage = asarray(originalImage)

with Image.open("AssetsVikingVillage_LR.png") as im:
    for i in range(6):
        start = time.process_time()
        im_resized = im.resize((width, height), resample=i, reducing_gap=1.0)
        print("time consumption of filter {} is {}".format(i, time.process_time() - start))
        im_resized = asarray(im_resized)
        ssim = structural_similarity(im_resized, originalImage, multichannel=True)
        print('ssim of filter {} is {}'.format(i, ssim))
        psnr = peak_signal_noise_ratio(originalImage, im_resized)
        print('psnr of filter {} is {}'.format(i, psnr))

with Image.open("AssetsVikingVillage_MR.png") as im:
    for i in range(6):
        start = time.process_time()
        im_resized = im.resize((width, height), resample=i, reducing_gap=1.0)
        print("time consumption of filter {} is {}".format(i, time.process_time() - start))
        im_resized = asarray(im_resized)
        ssim = structural_similarity(im_resized, originalImage, multichannel=True)
        print('ssim of filter {} is {}'.format(i, ssim))
        psnr = peak_signal_noise_ratio(originalImage, im_resized)
        print('psnr of filter {} is {}'.format(i, psnr))



from PIL import Image
from skimage.metrics import structural_similarity
from skimage.metrics import peak_signal_noise_ratio
from numpy import asarray
import time

width = 2400 
height = 1080

#https://hristog.github.io/running_python_scripts_on_your_android_device.html
#Image.NEAREST (0), Image.LANCZOS (1), Image.BILINEAR (2), Image.BICUBIC (3), Image.BOX (4), Image.HAMMING (5)

######################### half resolution test################################
remoteRT = Image.open("HalfResolutionTest/AssetsVikingVillage_MR.png")
reconstructedRemoteRT = remoteRT.resize((width, height), resample=2)
reconstructedRemoteRT.save("HalfResolutionTest/Recosntructed_AssetsVikingVillage_MR.png")

ECC_0_03 = Image.open("HalfResolutionTest/AssetsVikingVillage_LR_0_03.png")
ECC_0_03_gt = ECC_0_03.resize((width, height), resample=2)
ssim = structural_similarity(asarray(ECC_0_03_gt), asarray(reconstructedRemoteRT), multichannel=True)
print('Ecc0.03 ssim is {}'.format(ssim))
psnr = peak_signal_noise_ratio(asarray(ECC_0_03_gt), asarray(reconstructedRemoteRT))
print('Ecc0.03 psnr is {}'.format(psnr))
ECC_0_03_gt.save("HalfResolutionTest/Recosntructed_AssetsVikingVillage_LR_0_03.png")


ECC_0_04 = Image.open("HalfResolutionTest/AssetsVikingVillage_LR_0_04.png")
ECC_0_04_gt = ECC_0_04.resize((width, height), resample=2)
ssim = structural_similarity(asarray(ECC_0_04_gt), asarray(reconstructedRemoteRT), multichannel=True)
print('Ecc0.04 ssim is {}'.format(ssim))
psnr = peak_signal_noise_ratio(asarray(ECC_0_04_gt), asarray(reconstructedRemoteRT))
print('Ecc0.04 psnr is {}'.format(psnr))
ECC_0_03_gt.save("HalfResolutionTest/Recosntructed_AssetsVikingVillage_LR_0_04.png")


ECC_0_05 = Image.open("HalfResolutionTest/AssetsVikingVillage_LR_0_05.png")
ECC_0_05_gt = ECC_0_05.resize((width, height), resample=2)
ssim = structural_similarity(asarray(ECC_0_05_gt), asarray(reconstructedRemoteRT), multichannel=True)
print('Ecc0.05 ssim is {}'.format(ssim))
psnr = peak_signal_noise_ratio(asarray(ECC_0_05_gt), asarray(reconstructedRemoteRT))
print('Ecc0.05 psnr is {}'.format(psnr))
ECC_0_03_gt.save("HalfResolutionTest/Recosntructed_AssetsVikingVillage_LR_0_05.png")



######################### quarter resolution test################################
remoteRT = Image.open("QuarterResolutionTest/AssetsVikingVillage_LR.png")
reconstructedRemoteRT = remoteRT.resize((width, height), resample=2)
reconstructedRemoteRT.save("QuarterResolutionTest/Recosntructed_AssetsVikingVillage_LR.png")

ECC_0_03 = Image.open("QuarterResolutionTest/AssetsVikingVillage_LR_0_03.png")
ECC_0_03_gt = ECC_0_03.resize((width, height), resample=2)
ssim = structural_similarity(asarray(ECC_0_03_gt), asarray(reconstructedRemoteRT), multichannel=True)
print('Ecc0.03 ssim is {}'.format(ssim))
psnr = peak_signal_noise_ratio(asarray(ECC_0_03_gt), asarray(reconstructedRemoteRT))
print('Ecc0.03 psnr is {}'.format(psnr))
ECC_0_03_gt.save("QuarterResolutionTest/Recosntructed_AssetsVikingVillage_LR_0_03.png")

ECC_0_05 = Image.open("QuarterResolutionTest/AssetsVikingVillage_LR_0_05.png")
ECC_0_05_gt = ECC_0_05.resize((width, height), resample=2)
ssim = structural_similarity(asarray(ECC_0_05_gt), asarray(reconstructedRemoteRT), multichannel=True)
print('Ecc0.05 ssim is {}'.format(ssim))
psnr = peak_signal_noise_ratio(asarray(ECC_0_05_gt), asarray(reconstructedRemoteRT))
print('Ecc0.05 psnr is {}'.format(psnr))
ECC_0_03_gt.save("QuarterResolutionTest/Recosntructed_AssetsVikingVillage_LR_0_05.png")

ECC_0_07 = Image.open("QuarterResolutionTest/AssetsVikingVillage_LR_0_07.png")
ECC_0_07_gt = ECC_0_07.resize((width, height), resample=2)
ssim = structural_similarity(asarray(ECC_0_07_gt), asarray(reconstructedRemoteRT), multichannel=True)
print('Ecc0.07 ssim is {}'.format(ssim))
psnr = peak_signal_noise_ratio(asarray(ECC_0_07_gt), asarray(reconstructedRemoteRT))
print('Ecc0.07 psnr is {}'.format(psnr))
ECC_0_03_gt.save("QuarterResolutionTest/Recosntructed_AssetsVikingVillage_LR_0_07.png")

ECC_0_09 = Image.open("QuarterResolutionTest/AssetsVikingVillage_LR_0_09.png")
ECC_0_09_gt = ECC_0_09.resize((width, height), resample=2)
ssim = structural_similarity(asarray(ECC_0_09_gt), asarray(reconstructedRemoteRT), multichannel=True)
print('Ecc0.09 ssim is {}'.format(ssim))
psnr = peak_signal_noise_ratio(asarray(ECC_0_09_gt), asarray(reconstructedRemoteRT))
print('Ecc0.09 psnr is {}'.format(psnr))
ECC_0_03_gt.save("QuarterResolutionTest/Recosntructed_AssetsVikingVillage_LR_0_09.png")

ECC_0_11 = Image.open("QuarterResolutionTest/AssetsVikingVillage_LR_0_11.png")
ECC_0_11_gt = ECC_0_11.resize((width, height), resample=2)
ssim = structural_similarity(asarray(ECC_0_11_gt), asarray(reconstructedRemoteRT), multichannel=True)
print('Ecc0.11 ssim is {}'.format(ssim))
psnr = peak_signal_noise_ratio(asarray(ECC_0_11_gt), asarray(reconstructedRemoteRT))
print('Ecc0.11 psnr is {}'.format(psnr))
ECC_0_03_gt.save("QuarterResolutionTest/Recosntructed_AssetsVikingVillage_LR_0_11.png")
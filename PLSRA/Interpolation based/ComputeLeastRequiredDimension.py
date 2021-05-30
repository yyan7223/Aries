import math
import numpy as np
_W = 17
_V = 5 # phsical deistance
_D = 2400
_alpha = 0.45
_omega = math.degrees(math.atan(2 * _W / _V / _D))
_e = math.degrees(math.atan(_W / _V / 2))
m = 0.028
_omega0 = 1/48

E2SearchTime = 20
minimumFrameSize = 1000
optimalE2Ratio = 0
optimalE2Degree = 0
optimalD2 = 0
optimalD3 = 0

# _E1Ratio = [0.03, 0.04, 0.05] # half resolution
_E1Ratio = [0.05, 0.1, 0.15, 0.2, 0.25, 0.3, 0.35, 0.4, 0.45, 0.50] # quarter resolution

# input x in math.tan(x) and math.atan(x) must be in radians
# we can use math.degrees(x) and math.radians(x) to convert between degrees and radians
# https://docs.python.org/3/library/math.html

for _E1 in _E1Ratio:
    _E1Degree = math.degrees(math.atan(_E1 * _W / _V))
    print("E1 ratio is {0}, E1 degree is {1}".format(_E1, _E1Degree))
    S2 = (m * _E1Degree + _omega0) / _omega
    D2 = _D / S2
    print("remote layer resolution is {0} x {1}".format(int(D2), int(D2*_alpha)))
    print('')

for _E1 in _E1Ratio:
    _E1Degree = math.degrees(math.atan(_E1 * _W / _V))
    print("E1 ratio is {0}, E1 degree is {1}".format(_E1, _E1Degree))
    E2SearchList = np.linspace(_E1, 0.5, E2SearchTime)
    # search for optimal E2 for each E1
    for _E2 in E2SearchList:
        _E2Degree = math.degrees(math.atan(_E2 * _W / _V))
        S2 = (m * _E1Degree + _omega0) / _omega
        D2 = 2 * _D * math.tan(math.radians(_E2Degree)) * _V / S2 / _W
        S3 = (m * _E2Degree + _omega0) / _omega
        D3 = _D / S3
        totalFrameSize = (D2 * D2 *_alpha + D3 * D3 *_alpha) * 4 / 1024 / 1024
        if (totalFrameSize < minimumFrameSize):
            minimumFrameSize = totalFrameSize
            optimalE2Ratio = _E2
            optimalE2Degree = _E2Degree
            optimalD2 = D2
            optimalD3 = D3
    print("optimal E2 ratio is {0}, E2 degree is {1}".format(optimalE2Ratio, optimalE2Degree))
    print("middle layer resolution is {0} x {1}".format(int(optimalD2), int(optimalD2*_alpha)))
    print("outer layer resolution is {0} x {1}".format(int(optimalD3), int(optimalD3*_alpha)))
    print('')
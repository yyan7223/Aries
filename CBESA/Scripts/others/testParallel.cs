using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;

public class testParallel : MonoBehaviour
{
    Stopwatch stopWatch = new Stopwatch();
    void Start()
    {
        stopWatch.Start();
        for (int i = 0; i < 10000; i++)
        {
            for (int j = 0; j < 60000; j++)
            {
                int sum = 0;
                sum += i;
            }
        }
        stopWatch.Stop();
        print("NormalFor run " + stopWatch.ElapsedMilliseconds + " ms.");

        stopWatch.Reset();
        stopWatch.Start();
        Parallel.For(0, 10000, item =>
        {
            for (int j = 0; j < 60000; j++)
            {
                int sum = 0;
                sum += item;
            }
        });
        stopWatch.Stop();
        print("ParallelFor run " + stopWatch.ElapsedMilliseconds + " ms.");
    }
}

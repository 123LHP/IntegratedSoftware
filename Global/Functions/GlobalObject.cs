﻿using CommonLibrary.Manager;
using Halcon.Functions;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Global.Functions
{
    public static class GlobalObjectList
    {
        private static bool m_bIsPause = false;
        private static bool m_bIsStop = false;

        public static bool IsPause { get => m_bIsPause; }

        public static bool IsStop { get => m_bIsStop; }

        public static List<ProcessManager<IImageHalconObject>> ImageListObject = new List<ProcessManager<IImageHalconObject>>();//图像处理对象

        public static int SelectedSerial = -1;

        public static int SelectedServer = -1;

        public static int SelectedClient = -1;

        public static int SelectedImageIndex = 0;

        public delegate void Finish(HImage hImage, List<ShowObject> showObjects, List<ShowText> showTexts, string message, int index);
        public static Finish OnFinish;

        public static Action<int, RunStatus> SetRunStatus;

        public static void RunImageProcess(int start, int end, int runIndex)
        {
            if (m_bIsPause)
            {
                m_bIsPause = false;
            }
            else
            {
                ParameterizedThreadStart parameterized = new ParameterizedThreadStart(RunProcess);
                Thread thread = new Thread(parameterized);
                thread.IsBackground = true;
                int[] index = new int[3] { start, end, runIndex };
                thread.Start(index);
            }
        }

        public static void RunIndexProcess(int index)
        {
            RunImageProcess(0, ImageListObject[index].ProcessCount, index);
        }

        public static void PauseImageProcess()
        {
            m_bIsPause = true;
        }

        public static void StopImageProcess()
        {
            m_bIsStop = true;
        }

        private static void RunProcess(object index)
        {
            HImage curImage = new HImage();
            List<ShowObject> showObjects = new List<ShowObject>();
            List<ShowText> showTexts = new List<ShowText>();
            Stopwatch sw = new Stopwatch();
            int[] indexs = index as int[];
            int start = indexs[0];
            int end = indexs[1];
            int runIndex = indexs[2];
            string message = "";
            for (int i = start; i < end; i++)
            {
                if (SetRunStatus != null)
                {
                    SetRunStatus(i, RunStatus.Wait);
                }
            }
            sw.Start();
            for (int i = start; i < end; i++)
            {
                if (m_bIsStop)
                {
                    break;
                }
                if (m_bIsPause)
                {
                    while (m_bIsPause)
                    {
                        Thread.Sleep(2);
                        if (m_bIsStop)
                        {
                            break;
                        }
                    }
                }
                if (i == 0)
                {
                    IImageHalconObject imageHalconObject = ImageListObject[runIndex].GetProcessByIndex(i);
                    imageHalconObject.ImageHandle(ref curImage, ref showObjects, ref showTexts);//处理
                    if (SetRunStatus != null)
                    {
                        if (imageHalconObject.IsRunOK)
                        {
                            SetRunStatus(i, RunStatus.OK);
                        }
                        else
                        {
                            SetRunStatus(i, RunStatus.NG);
                            message = $"工具{i + 1}执行失败,{imageHalconObject.ErrorMessage}\r\n";
                            break;
                        }
                    }
                }
                else
                {
                    IImageHalconObject previousImageHalconObject = ImageListObject[runIndex].GetProcessByIndex(i - 1);
                    IImageHalconObject currentImageHalconObject = ImageListObject[runIndex].GetProcessByIndex(i);
                    if (previousImageHalconObject.IsRunOK)//处理结果
                    {
                        bool isBinding = true;
                        try
                        {
                            for (int j = 0; j < currentImageHalconObject.GetDataManager.DataBindingCount; j++)//获取绑定数据
                            {
                                if (m_bIsStop)
                                {
                                    break;
                                }
                                if (m_bIsPause)
                                {
                                    while (m_bIsPause)
                                    {
                                        Thread.Sleep(2);
                                        if (m_bIsStop)
                                        {
                                            break;
                                        }
                                    }
                                }
                                DataBinding dataBinding = currentImageHalconObject.GetDataManager.GetDataBinding(j);
                                if (dataBinding != null)
                                {
                                    IImageHalconObject bindingImageHalconObject = ImageListObject[runIndex].GetProcessByIndex(dataBinding.DataSourceIndex);
                                    switch (dataBinding.DataType)
                                    {
                                        case DataType.INT:
                                            currentImageHalconObject.GetDataManager.SetInputInt(dataBinding.DataName, bindingImageHalconObject.GetDataManager.GetOutputInt(dataBinding.DataSourceName));
                                            break;
                                        case DataType.INTARRAY:
                                            currentImageHalconObject.GetDataManager.SetInputIntArray(dataBinding.DataName, bindingImageHalconObject.GetDataManager.GetOutputIntArray(dataBinding.DataSourceName));
                                            break;
                                        case DataType.DOUBLE:
                                            currentImageHalconObject.GetDataManager.SetInputDouble(dataBinding.DataName, bindingImageHalconObject.GetDataManager.GetOutputDouble(dataBinding.DataSourceName));
                                            break;
                                        case DataType.DOUBLEARRAY:
                                            currentImageHalconObject.GetDataManager.SetInputDoubleArray(dataBinding.DataName, bindingImageHalconObject.GetDataManager.GetOutputDoubleArray(dataBinding.DataSourceName));
                                            break;
                                        default:
                                            break;
                                    }
                                }
                                else
                                {
                                    message = $"工具{i + 1}执行失败，没有正确配置输入设置，请检查\r\n";
                                    isBinding = false;
                                    break;
                                }
                            }
                            if (isBinding)
                            {
                                currentImageHalconObject.ImageHandle(ref curImage, ref showObjects, ref showTexts);//处理
                                if (SetRunStatus != null)
                                {
                                    if (currentImageHalconObject.IsRunOK)
                                    {
                                        SetRunStatus(i, RunStatus.OK);
                                    }
                                    else
                                    {
                                        SetRunStatus(i, RunStatus.NG);
                                        message = $"工具{i + 1}执行失败，{currentImageHalconObject.ErrorMessage}\r\n";
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                        catch
                        {
                            message = $"工具{i + 1}执行失败，没有正确配置输入设置，请检查\r\n";
                            isBinding = false;
                            break;
                        }
                    }
                    else
                    {
                        message = $"工具{i}执行失败,{previousImageHalconObject.ErrorMessage}\r\n";
                        break;
                    }
                }
            }
            sw.Stop();
            message += $"执行完成，耗时：{sw.ElapsedMilliseconds}ms";
            OnFinish.Invoke(curImage, showObjects, showTexts, message, runIndex);
            m_bIsStop = false;
            m_bIsPause = false;
            curImage.Dispose();
        }
    }
}

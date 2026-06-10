using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using UyduArayuz_1.Models;
namespace UyduArayuz_1.ViewModels
{
    public class AttitudeViewModel
    {
        private double _pitch;
        public double Pitch
        {
            get => _pitch;
            set { _pitch = value;  }
        }

        private double _roll;
        public double Roll
        {
            get => _roll;
            set { _roll = value; }
        }

        private double _yaw;
        public double Yaw
        {
            get => _yaw;
            set { _yaw = value; }
        }


        public void UpdateAttitude(double yaw, double pitch, double roll)
        {
            Yaw = yaw;
            Pitch = pitch;
            Roll = roll;
            
        }

        // İleride eklenebilecek özellikler
        public void ResetOrientation() { Pitch = Roll = Yaw = 0; }


    }
}

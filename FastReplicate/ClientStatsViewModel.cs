using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using Sandbox.Engine.Multiplayer;
using Torch;
using VRage.Game;

namespace FastReplicate
{
    public class ClientStatsViewModel : ViewModel
    {
        private readonly ulong _id;

        public ClientStatsViewModel(ulong id)
        {
            _id = id;
        }

        public string Name => MyMultiplayer.Static?.GetMemberName(_id) ?? ("ID:" + _id);

        private int _staticReplicables;

        public int StaticReplicables
        {
            get => _staticReplicables;
            set => SetValue(ref _staticReplicables, value);
        }

        private int _staticReplicablesUnsent;

        public int StaticReplicablesUnsent
        {
            get => _staticReplicablesUnsent;
            set => SetValue(ref _staticReplicablesUnsent, value);
        }

        private int _streamingRoots;

        public int StreamingRoots
        {
            get => _streamingRoots;
            set => SetValue(ref _streamingRoots, value);
        }

        private int _staticPacketsSent;

        public int StaticPacketsSent
        {
            get => _staticPacketsSent;
            set => SetValue(ref _staticPacketsSent, value);
        }

        private int _streamingPacketsSent;

        public int StreamingPacketsSent
        {
            get => _streamingPacketsSent;
            set => SetValue(ref _streamingPacketsSent, value);
        }

        private long _totalBitsSent;

        public long TotalBitsSent
        {
            get => _totalBitsSent;
            set => SetValue(ref _totalBitsSent, value);
        }

        private int _totalRoots;
        public int TotalRoots
        {
            get => _totalRoots;
            set => SetValue(ref _totalRoots, value);
        }

        #region Averages

        private long _totSentStatic, _totStaticRemaining, _totSentStreaming, _totBitsSent, _totStaticReplicables, _totStreamingReplicables;

        private float _staticPacketsPerSecond;

        public float StaticPacketsPerSecond
        {
            get => _staticPacketsPerSecond;
            private set => SetValue(ref _staticPacketsPerSecond, value);
        }

        private float _streamingPacketsPerSecond;

        public float StreamingPacketsPerSecond
        {
            get => _streamingPacketsPerSecond;
            private set => SetValue(ref _streamingPacketsPerSecond, value);
        }

        private float _sendBitsPerSecond;

        public float SendBitsPerSecond
        {
            get => _sendBitsPerSecond;
            private set => SetValue(ref _sendBitsPerSecond, value);
        }

        private float _avgStaticReplicables;

        public float AverageStaticReplicables
        {
            get => _avgStaticReplicables;
            private set => SetValue(ref _avgStaticReplicables, value);
        }

        private float _avgStaticReplicablesRemaining;

        public float AverageStaticReplicablesRemaining
        {
            get => _avgStaticReplicablesRemaining;
            private set => SetValue(ref _avgStaticReplicablesRemaining, value);
        }


        private float _avgStreamingRoots;

        public float AverageStreamingRoots
        {
            get => _avgStreamingRoots;
            private set => SetValue(ref _avgStreamingRoots, value);
        }

        private int _averageTick;

        public void TickAverages()
        {
            _totSentStatic += StaticPacketsSent;
            _totSentStreaming += StreamingPacketsSent;
            _totBitsSent += TotalBitsSent;
            _totStaticReplicables += StaticReplicables;
            _totStaticRemaining += StaticReplicablesUnsent;
            _totStreamingReplicables += StreamingRoots;
            _averageTick++;
            if (_averageTick > MyEngineConstants.UPDATE_STEPS_PER_SECOND)
            {
                OnPropertyChanged(nameof(Name));
                var dt = _averageTick * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;

                AverageStreamingRoots =_totStreamingReplicables / (float) _averageTick;
                AverageStaticReplicables =_totStaticReplicables / (float) _averageTick;
                AverageStaticReplicablesRemaining = _totStaticRemaining / (float) _averageTick;

                SendBitsPerSecond = _totBitsSent / dt;
                StaticPacketsPerSecond = _totSentStatic / dt;
                StreamingPacketsPerSecond = _totSentStreaming / dt;

                _totSentStatic = 0;
                _totSentStreaming = 0;
                _totBitsSent = 0;
                _totStaticReplicables = 0;
                _totStreamingReplicables = 0;
                _totStaticRemaining = 0;

                _averageTick = 0;
            }
        }

        #endregion
    }

    public class BitrateValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(string))
                throw new ArgumentOutOfRangeException(nameof(targetType));

            if (value == null)
                return "";

            var bitrate = (float) value;
            if (bitrate > 1024 * 1024)
                return $"{bitrate / 1024 * 1024:F2} Mbps";
            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (bitrate > 1024)
                return $"{bitrate / 1024:F2} Kbps";
            return $"{bitrate:F2} bps";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
﻿//-----------------------------------------------------------------------
// <copyright file="ColumnChartViewModel.cs" company="Wohs">
//     Missing Copyright information from a valid stylecop.json file.
// </copyright>
//-----------------------------------------------------------------------

namespace Lurker.UI.ViewModels
{
    using LiveCharts;
    using LiveCharts.Wpf;
    using System.Collections.Generic;

    public class ColumnChartViewModel : ChartViewModelBase
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="PieChartViewModel"/> class.
        /// </summary>
        public ColumnChartViewModel(string[] labels)
            : base(labels)
        {
        }

        #endregion

        #region Methods

        /// <summary>
        /// Adds the specified label.
        /// </summary>
        /// <param name="label">The label.</param>
        /// <param name="value">The value.</param>
        public void Add(string title, IEnumerable<double> values)
        {
            this.SeriesCollection.Add(new ColumnSeries
            {
                Title = title,
                Values = new ChartValues<double>(values)
            });
        }

        #endregion
    }
}

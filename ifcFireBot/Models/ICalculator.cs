// Models/ICalculator.cs
using System.Collections.Generic;
using Xbim.Ifc4.Interfaces;

namespace ifcFireBot.Models
{
    public interface ICalculator
    {
        string Calculate(List<IIfcProduct> elements, Intent intent, IOutputWriter output);
    }
}
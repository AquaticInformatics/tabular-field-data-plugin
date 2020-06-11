using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TabularCsv
{
    public static class ColumnDecorator
    {
        public static List<ColumnDefinition> GetNamedColumns(object item, string baseName = "")
        {
            var columnDefinitions = new List<ColumnDefinition>();

            if (item == null)
                return columnDefinitions;

            if (item is ColumnDefinition itemDefinition)
            {
                columnDefinitions.Add(itemDefinition);
                itemDefinition.SetNamePrefix(baseName);
            }

            var type = item.GetType();
            var baseColumnType = typeof(ColumnDefinition);
            var baseListType = typeof(List<>);
            var propertyDefinitionType = typeof(PropertyDefinition);

            void AddNamedColumn(ColumnDefinition column, string propertyName)
            {
                var columnPropertyType = column.GetType();

                if (propertyDefinitionType.IsAssignableFrom(columnPropertyType))
                {
                    columnDefinitions.Add(column);
                    column.SetNamePrefix($"{baseName}.{propertyName}");
                }
                else
                {
                    columnDefinitions.AddRange(GetNamedColumns(column, $"{baseName}.{propertyName}"));
                }
            }

            var candidateProperties = type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && p.CanWrite)
                .ToList();

            var columnProperties = candidateProperties
                .Where(p => baseColumnType.IsAssignableFrom(p.PropertyType))
                .ToList();

            foreach (var columnProperty in columnProperties)
            {
                var getMethod = columnProperty.GetGetMethod(false);

                if (getMethod == null)
                    continue;

                var column = getMethod.Invoke(item, null) as ColumnDefinition;

                if (column == null)
                    continue;

                AddNamedColumn(column, columnProperty.Name);
            }

            var columnCollectionProperties = candidateProperties
                .Where(p => p.PropertyType.IsGenericType && baseListType == p.PropertyType.GetGenericTypeDefinition())
                .ToList();

            foreach (var columnCollectionProperty in columnCollectionProperties)
            {
                var getMethod = columnCollectionProperty.GetGetMethod(false);

                if (getMethod == null)
                    continue;

                var list = getMethod.Invoke(item, null) as IEnumerable;

                if (list == null)
                    continue;

                var index = 1;
                foreach (var listItem in list)
                {
                    if (listItem is ColumnDefinition column)
                    {
                        AddNamedColumn(column, $"{columnCollectionProperty.Name}[{index}]");
                    }

                    ++index;
                }
            }

            return columnDefinitions;
        }
    }
}
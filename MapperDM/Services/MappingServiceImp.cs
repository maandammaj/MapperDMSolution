using MapperDM.Common.Exceptions;
using MapperDM.Services.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MapperDM
{
    public class MappingServiceImp : IMappingService
    {

        public async Task<Result<TDestination>> Map<TSource, TDestination>(TSource source) where TDestination : new()
        {
            try
            {
                if (source == null)
                {
                    return await Result<TDestination>.FaildAsync(false, "Source model is Null");
                }

                TDestination destination = new TDestination();
                InitializeCollections(destination);
                await MapProperties(source, destination);

                return await Result<TDestination>.SuccessAsync(destination, "Model mapped Successfully", true);
            }
            catch (Exception ex)
            {
                return await Result<TDestination>.FaildAsync(false, $"Mapping failed: {ex.Message}");
            }
        }

        private async Task MapProperties(object source, object destination)
        {
            PropertyInfo[] sourceProperties = source.GetType().GetProperties();
            PropertyInfo[] destinationProperties = destination.GetType().GetProperties();

            foreach (PropertyInfo sourceProperty in sourceProperties)
            {
                PropertyInfo destinationProperty = Array.Find(destinationProperties, p => p.Name == sourceProperty.Name);

                if (destinationProperty != null && destinationProperty.CanWrite)
                {
                    if (typeof(IEnumerable).IsAssignableFrom(destinationProperty.PropertyType) && destinationProperty.PropertyType != typeof(string))
                    {
                        var sourceCollection = (IEnumerable)sourceProperty.GetValue(source);
                        if (sourceCollection != null)
                        {
                            var destinationCollectionType = destinationProperty.PropertyType.GetGenericArguments()[0];
                            var destinationCollection = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(destinationCollectionType));

                            foreach (var sourceItem in sourceCollection)
                            {
                                var destinationItem = Activator.CreateInstance(destinationCollectionType);
                                await MapProperties(sourceItem, destinationItem);
                                destinationCollection.Add(destinationItem);
                            }

                            destinationProperty.SetValue(destination, destinationCollection);
                        }
                    }
                    else
                    {
                        destinationProperty.SetValue(destination, sourceProperty.GetValue(source));
                    }
                }
            }
        }

        private void InitializeCollections(object destination)
        {
            PropertyInfo[] destinationProperties = destination.GetType().GetProperties();

            foreach (PropertyInfo destinationProperty in destinationProperties)
            {
                if (typeof(IEnumerable).IsAssignableFrom(destinationProperty.PropertyType) && destinationProperty.PropertyType != typeof(string))
                {
                    var destinationCollectionType = destinationProperty.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>)
                        ? typeof(List<>).MakeGenericType(destinationProperty.PropertyType.GetGenericArguments()[0])
                        : destinationProperty.PropertyType;
                    var destinationCollection = Activator.CreateInstance(destinationCollectionType);
                    destinationProperty.SetValue(destination, destinationCollection);
                }
            }
        }

        public async Task<Result<List<TDestination>>> MapCollection<TSource, TDestination>(IEnumerable<TSource> sourceCollection) where TDestination : new()
        {

            var res = new List<TDestination>();
            foreach (var item in sourceCollection)
            {
                var mappedResult = await Map<TSource, TDestination>(item);
                if (mappedResult.IsSuccess)
                {
                    res.Add(mappedResult.Data);
                }
                else
                {
                    return await Result<List<TDestination>>.FaildAsync(false, mappedResult.Message);
                }
            }
            return await Result<List<TDestination>>.SuccessAsync(res, "Collection Mapped Successfully", true);
        }

    }
}

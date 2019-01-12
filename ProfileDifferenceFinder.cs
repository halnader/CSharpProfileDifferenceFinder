    public static class ProfileDifferenceFinderHelper
    {
        private static string[] ListOfPropertiesToAvoid = new string[]
        {
            "Id",
            "DateTimeStamp",
        };

        public static Task<string> FindPropertyDifferences(object obj1, object obj2, Type comparatorType, object obj3, object obj4, Type extraComparator)
        {
            return Task.Factory.StartNew(() =>
              {
                  string changes = null;

                  PropertyInfo[] properties = comparatorType.GetProperties();
                  changes += DetermineDifferences(properties, obj1, obj2, comparatorType);

                  properties = extraComparator.GetProperties();
                  changes += DetermineDifferences(properties, obj3, obj4, extraComparator);

                  return changes;
              });
        }

        private static List<string> FindDifferenceBetweenNonSystemTypes(List<string> changes, object obj1, object obj2, Type comparatorType, PropertyInfo pi = null, bool? printHeader = null, int? lineNumber = null, string header = null)
        {
            PropertyInfo[] properties = comparatorType.GetProperties();

            foreach (PropertyInfo prop in properties)
            {
                if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>) && (prop.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>) || !ListOfPropertiesToAvoid.Any(a => prop.Name.Contains(a))))
                {
                    try
                    {
                        dynamic value1 = comparatorType.GetProperty(prop.Name).GetValue(obj1, null);
                        dynamic value2 = comparatorType.GetProperty(prop.Name).GetValue(obj2, null);

                        var collection1 = new List<dynamic>(value1);
                        var collection2 = new List<dynamic>(value2);

                        if (collection1.Count >= collection2.Count)
                        {
                            changes = FindDifferenceBetweenElementsInList(changes, collection1, collection2, prop, false);
                        }
                        else
                        {
                            changes = FindDifferenceBetweenElementsInList(changes, collection2, collection1, prop, true);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
                else if (!ListOfPropertiesToAvoid.Any(a => prop.Name.Contains(a)) && !prop.GetGetMethod().IsVirtual && prop.PropertyType.Namespace == "System")
                {
                    try
                    {
                        object value1 = comparatorType.GetProperty(prop.Name).GetValue(obj1, null);
                        object value2 = comparatorType.GetProperty(prop.Name).GetValue(obj2, null);

                        if (value1 != value2 && (value1 == null || !value1.Equals(value2)))
                        {
                            if (printHeader != null)
                            {
                                if (!(bool)printHeader)
                                {
                                    changes.Add(string.Format("\n{0}, Item(s) {1} list", pi.Name, header));
                                    printHeader = true;
                                }

                                changes.Add(string.Format("--> {0} changed from \"{1}\" to \"{2}\" on line {3}", prop.Name, value1, value2, lineNumber + 1));
                            }
                            else
                                changes.Add(string.Format("\n{0} changed from \"{1}\" to \"{2}\"", pi.Name != null ? pi.Name : prop.Name, value1 == null ? "Empty" : value1, value2 == null ? "Empty" : value2));
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
                else if (!ListOfPropertiesToAvoid.Any(a => prop.Name.Contains(a)) && !prop.GetGetMethod().IsVirtual)
                {
                    try
                    {
                        object value1 = comparatorType.GetProperty(prop.Name).GetValue(obj1, null);
                        object value2 = comparatorType.GetProperty(prop.Name).GetValue(obj2, null);

                        FindDifferenceBetweenNonSystemTypes(changes, value1, value2, prop.PropertyType);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            return changes;
        }

        private static List<string> FindDifferenceBetweenElementsInList(List<string> changes, List<dynamic> LargerCollection, List<dynamic> Smallercollection, PropertyInfo pi, bool ElementsAdded)
        {
            bool foundChange = false;
            bool modificationsPerformed = false;

            string ListChangesHeader = "modified and/or removed from";
            string ItemChangesHeader = "removed from";
            string ItemStatusMessage = "removed";

            if (ElementsAdded)
            {
                ListChangesHeader = "modified and/or added to";
                ItemChangesHeader = "added to";
                ItemStatusMessage = "added";
            }

            for (int i = 0; i < Smallercollection.Count; i++)
            {
                Type type = Smallercollection[i].GetType();
                PropertyInfo[] collectionAProp = (type).GetProperties();

                foreach (PropertyInfo prop in collectionAProp)
                {
                    if (!ListOfPropertiesToAvoid.Any(a => prop.Name.Contains(a)) && !prop.GetGetMethod().IsVirtual && prop.PropertyType.Namespace == "System")
                    {
                        object val1 = type.GetProperty(prop.Name).GetValue(LargerCollection[i], null);
                        object val2 = type.GetProperty(prop.Name).GetValue(Smallercollection[i], null);

                        if (val1.ToString() != val2.ToString())
                        {
                            if (!foundChange)
                            {
                                changes.Add(string.Format("\n{0}, Item(s) {1} list", pi.Name, ListChangesHeader));
                                foundChange = true;
                                modificationsPerformed = true;
                            }
                            if (ElementsAdded)
                            {
                                changes.Add(string.Format("--> {0} changed from \"{1}\" to \"{2}\" on line {3}", prop.Name, val2, val1, i + 1));
                            }
                            else
                                changes.Add(string.Format("--> {0} changed from \"{1}\" to \"{2}\" on line {3}", prop.Name, val1, val2, i + 1));
                        }
                    }
                    else if (!ListOfPropertiesToAvoid.Any(a => prop.Name.Contains(a)))
                    {
                        int detectingChanges = changes.Count;
                        try
                        {
                            dynamic val1 = type.GetProperty(prop.Name).GetValue(LargerCollection[i], null);
                            dynamic val2 = type.GetProperty(prop.Name).GetValue(Smallercollection[i], null);

                            changes = FindDifferenceBetweenNonSystemTypes(changes, val1, val2, prop.PropertyType, prop, foundChange, i, ListChangesHeader);
                        }
                        catch (Exception)
                        {
                        }
                        if (detectingChanges < changes.Count)
                        {
                            modificationsPerformed = true;
                        }
                    }
                }
            }

            for (int i = Smallercollection.Count; i < LargerCollection.Count; i++)
            {
                Type type = LargerCollection[i].GetType();
                PropertyInfo[] collectionAProp = (type).GetProperties();

                string removedOrAddedItem = null;
                bool firstElement = true;

                foreach (PropertyInfo prop in collectionAProp)
                {
                    if (!ListOfPropertiesToAvoid.Any(a => prop.Name.Contains(a)))
                    {
                        object val1 = type.GetProperty(prop.Name).GetValue(LargerCollection[i], null);

                        if (prop.GetGetMethod().IsVirtual)
                        {
                            try
                            {
                                Type typeB = prop.PropertyType;
                                PropertyInfo[] collectionBProp = (typeB).GetProperties();

                                bool firstSubElement = true;

                                foreach (PropertyInfo subProp in collectionBProp)
                                {
                                    if (!ListOfPropertiesToAvoid.Any(a => subProp.Name.Contains(a)))
                                    {
                                        object val2 = typeB.GetProperty(subProp.Name).GetValue(val1, null);
                                        if (firstSubElement)
                                        {
                                            removedOrAddedItem += subProp.Name + ": " + val2.ToString();
                                            firstSubElement = false;
                                        }
                                        else
                                            removedOrAddedItem += ", " + subProp.Name + ": " + val2.ToString();
                                    }
                                }
                            }
                            catch (Exception)
                            {
                            }
                        }
                        else
                        {
                            if (firstElement)
                            {
                                removedOrAddedItem += prop.Name + ": " + val1.ToString();
                                firstElement = false;
                            }
                            else
                                removedOrAddedItem += ", " + prop.Name + ": " + val1.ToString();
                        }
                    }
                }

                if (!modificationsPerformed)
                {
                    changes.Add(string.Format("\n{0}, Item(s) {1} list", pi.Name, ItemChangesHeader));
                    modificationsPerformed = true;
                }

                changes.Add(string.Format("--> {0} has been {1}", removedOrAddedItem, ItemStatusMessage));
            }

            return changes;
        }

        private static string DetermineDifferences(PropertyInfo[] properties, object obj1, object obj2, Type comparatorType)
        {
            List<string> changes = new List<string>();
            foreach (PropertyInfo pi in properties)
            {
                if (pi.PropertyType.IsGenericType && pi.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>) && (pi.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>) || !ListOfPropertiesToAvoid.Any(a => pi.Name.Contains(a))))
                {
                    try
                    {
                        dynamic value1 = comparatorType.GetProperty(pi.Name).GetValue(obj1, null);
                        dynamic value2 = comparatorType.GetProperty(pi.Name).GetValue(obj2, null);

                        var collection1 = new List<dynamic>(value1);
                        var collection2 = new List<dynamic>(value2);

                        if (collection1.Count >= collection2.Count)
                        {
                            changes = FindDifferenceBetweenElementsInList(changes, collection1, collection2, pi, false);
                        }
                        else
                        {
                            changes = FindDifferenceBetweenElementsInList(changes, collection2, collection1, pi, true);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
                else if (!ListOfPropertiesToAvoid.Any(a => pi.Name.Contains(a)) && !pi.GetGetMethod().IsVirtual && pi.PropertyType.Namespace == "System")
                {
                    try
                    {
                        object value1 = comparatorType.GetProperty(pi.Name).GetValue(obj1, null);
                        object value2 = comparatorType.GetProperty(pi.Name).GetValue(obj2, null);

                        if (value1 != value2 && (value1 == null || !value1.Equals(value2)))
                        {
                            changes.Add(string.Format("\n{0} changed from \"{1}\" to \"{2}\"", pi.Name, value1 == null ? "Empty" : value1, value2 == null ? "Empty" : value2));
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
                else if (!ListOfPropertiesToAvoid.Any(a => pi.Name.Contains(a)))
                {
                    try
                    {
                        object value1 = comparatorType.GetProperty(pi.Name).GetValue(obj1, null);
                        object value2 = comparatorType.GetProperty(pi.Name).GetValue(obj2, null);

                        FindDifferenceBetweenNonSystemTypes(changes, value1, value2, pi.PropertyType, pi);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            string stringOfChanges = null;

            foreach (string st in changes)
            {
                stringOfChanges += st + "\n";
            }

            return stringOfChanges;
        }
    }
package benchmarker;

import java.util.List;

public abstract class Utils {

  public static String encodeToOneLine(String s) {
    return (s == null ? "" : s.replace('\n', (char)31));
  }
  public static String decodeFromOneLine(String s) {
    return s.replace((char)31, '\n');
  }

  public static String concatenateListOfStrings(List<String> list) {
    StringBuilder s = new StringBuilder();
    for (String el: list) if (el != null) s.append(el);
    return s.toString();
  }

}

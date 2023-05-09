for curr_complexity in {4..9}
do
  files=$(eval echo "unique_equations_{4..$curr_complexity}.txt");
  cat $files > unique_equations_${curr_complexity}_cumulative.txt
done

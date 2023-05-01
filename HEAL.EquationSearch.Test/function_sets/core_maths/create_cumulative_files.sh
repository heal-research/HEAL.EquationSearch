for curr_complexity in {1..10}
do
  files=$(eval echo "unique_equations_{1..$curr_complexity}.txt");
  cat $files > unique_equations_${curr_complexity}_cumulative.txt
done

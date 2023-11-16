set terminal pdf size 10cm,8cm

set datafile separator ';'

set xlabel "len"
set ylabel "DL"
set output "plot.pdf"
set yrange [-1400:-1000]
plot 'RAR_eqs_logexppow_len30_optimized_autodiffGrad_top1_over_len.txt' using "len":"DL" title "autodiff",\
     'RAR_eqs_logexppow_len30_optimized_numericGrad_top1_over_len.txt' using "len":"DL" title "numeric"

set ylabel "nll"
plot 'RAR_eqs_logexppow_len30_optimized_autodiffGrad_top1_over_len.txt' using "len":"nll" title "autodiff",\
     'RAR_eqs_logexppow_len30_optimized_numericGrad_top1_over_len.txt' using "len":"nll" title "numeric"

unset yrange
set ylabel "restarts "
plot 'RAR_eqs_logexppow_len30_optimized_autodiffGrad_top1_over_len.txt' using "len":"restarts" title "autodiff",\
     'RAR_eqs_logexppow_len30_optimized_numericGrad_top1_over_len.txt' using "len":"restarts" title "numeric"


set ylabel "num best / restarts"
set logscale y
plot 'RAR_eqs_logexppow_len30_optimized_autodiffGrad_top1_over_len.txt' using "len":(column("restartNumBest")/column("restarts")) title "autodiff",\
     'RAR_eqs_logexppow_len30_optimized_numericGrad_top1_over_len.txt' using "len":(column("restartNumBest")/column("restarts")) title "numeric"



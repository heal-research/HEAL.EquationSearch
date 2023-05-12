
# additional info from Harry:
# e_loggbar = e_gbar / (gbar * np.log(10.))
# e_loggobs = e_gobs / (gobs * np.log(10.))
# sigma2_tot = e_loggobs**2 + (gobs1_diff*e_loggbar)**2
# negloglike = 0.5 * np.sum((np.log10(gobs) - np.log10(gobs1))**2 ./ sigma2_tot + np.log(2.* np.pi * sigma2_tot))
  
  
data <- read.csv2("E:/reps/HEAL.EquationSearch/HEAL.EquationSearch.Test/RAR.csv", sep=',', dec='.')
data$log_gbar <- log10(data$gbar)
data$log_gobs <- log10(data$gobs)
data$e_log_gbar <- data$e_gbar / (data$gbar * log(10))
data$e_log_gobs <- data$e_gobs / (data$gobs * log(10))

scatter.smooth(data$log_gbar, data$log_gobs, family = "symmetric")
plot(data$log_gbar, data$log_gobs)

model <- loess(log_gobs ~ log_gbar, data=data, span = 1.0, degree = 2,
                         family = "symmetric", weights = data$e_log_gobs)


smoothed <- predict(model, sort(data$log_gbar))
dsmoothed <- diff(smoothed) / diff(sort(data$log_gbar))
plot(sort(data$log_gbar), smoothed)
# plot(sort(data$log_gbar[2:2696]), dsmoothed)


min_log_gbar <- min(data$log_gbar)
max_log_gbar <- max(data$log_gbar)

log_gbar <- seq(1,10000) / 10000 * (max_log_gbar - min_log_gbar) + min_log_gbar
smoothed <- predict(model, log_gbar)


dsmoothed <- diff(smoothed) / diff(log_gbar)

plot(log_gbar[1:9999], dsmoothed)



data$df_dlogbar <- sapply(data$log_gbar, FUN=function(x) {dsmoothed[min(which(log_gbar>=x))] })


data$stot <- sqrt(data$e_log_gobs**2 + (data$df_dlogbar*data$e_log_gbar)**2)

write.csv(data, "E:/reps/HEAL.EquationSearch/HEAL.EquationSearch.Test/RAR_sigma.csv", sep=',', dec='.', quote = FALSE, row.names = FALSE)


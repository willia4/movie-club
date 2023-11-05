const path = require('path');
const gulp = require('gulp');
const {  parallel } = require('gulp');
const sass = require('gulp-sass')(require('sass'));
const sourcemaps = require('gulp-sourcemaps');
const rename = require("gulp-rename");

function customBootstrap() {
    return gulp.src('./css_dev/*.scss')
        .pipe(sourcemaps.init())
        .pipe(sass({outputStyle: 'expanded'}).on('error', sass.logError))
        .pipe(sourcemaps.write('./maps'))
        .pipe(rename(path => {
            return {
                dirname: path.dirname,
                basename: 'bootstrap.min',
                extname: path.extname
            }
        }))
        .pipe(gulp.dest('./wwwroot/lib/bootstrap/css'));
}

function copyBootstrapJavascript() {
    return gulp.src('./node_modules/bootstrap/dist/js/*')
        .pipe(gulp.dest('./wwwroot/lib/bootstrap/js'))
}


exports.default = parallel(customBootstrap, copyBootstrapJavascript);

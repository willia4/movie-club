const path = require('path');
const gulp = require('gulp');
const { series, parallel } = require('gulp');
const sass = require('gulp-sass')(require('sass'));
const sourcemaps = require('gulp-sourcemaps');
const cleanCSS = require('gulp-clean-css');
const rename = require("gulp-rename");
const rollup = require('rollup-stream');
const source = require('vinyl-source-stream');
const minify = require('gulp-minify');

function compileBootstrapCss() {
    return gulp.src('./vendor/bootstrap-5.3.2/scss/**/*.scss')
        .pipe(sourcemaps.init())
        .pipe(sass().on('error', sass.logError))
        .pipe(sourcemaps.write('./maps'))
        .pipe(cleanCSS({}))
        .pipe(rename(path => {
            return {
                dirname: path.dirname,
                basename: path.basename + '.min',
                extname: path.extname
            }
        }))
        .pipe(gulp.dest('./wwwroot/lib/bootstrap/css'));
}

function minifyBootstrapJavascript() {
    return gulp.src('./vendor/bootstrap-5.3.2/dist/js/*.js')
        .pipe(minify({
            ext: {
                src: '.js',
                min: '.js'
            },
            mangle: false
        }))
        .pipe(gulp.dest('./wwwroot/lib/bootstrap/js'))
}


exports.default = parallel(compileBootstrapCss, minifyBootstrapJavascript);
